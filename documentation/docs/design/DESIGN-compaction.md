# Design: Conversation Compaction

## Status

**Proposed** — not yet implemented. This document captures the design for future implementation.

## Problem

The agent loop currently manages context with two blunt mechanisms:

1. **Per-result truncation** — `TruncateToolResult()` clips individual tool outputs at `MaxToolResultChars` (40,000 chars). This happens at ingestion time and is irreversible.
2. **History trimming** — `TrimConversationHistory()` does a hard `RemoveRange(0, removeCount)` when `_messages.Count > MaxConversationMessages` (50). No summarization, no context preservation — old messages simply vanish.

This is problematic for multi-step tasks where earlier instructions, scoring criteria, and intermediate results are critical context. For example, when the user says "Execute the job search prompt from search-prompt.txt", the search criteria read from the file are essential for scoring decisions later. If those messages are trimmed, Claude loses the ability to apply the correct rubric.

There is also no token awareness. The 50-message limit is a count-based proxy that doesn't account for message size — 50 messages containing short text exchanges use far fewer tokens than 50 messages containing large tool results.

## Reference: OpenClaw

The [openclaw](https://github.com/nicobailey/openclaw) project (TypeScript) implements a sophisticated multi-layer compaction system:

- **Token estimation** — `estimateTokens()` on each message, accumulated totals
- **Adaptive chunking** — `splitMessagesByTokenShare()` divides messages into equal-token chunks; `chunkMessagesByMaxTokens()` splits by max tokens per chunk
- **Multi-stage summarization** — `summarizeInStages()`: split messages into N parts, summarize each, then merge summaries with "Merge these partial summaries into a single cohesive summary. Preserve decisions, TODOs, open questions, and any constraints."
- **Fallback strategy** — full summarization → partial (excluding oversized messages) → annotation-only ("Context contained N messages. Summary unavailable.")
- **Context window guard** — model context window awareness (200K default), hard minimum 16K, warning at 32K
- **Compaction reserve** — 20K tokens kept reserved for the compaction API call itself
- **Context pruning** — separate from compaction; truncates oversized tool results keeping head+tail, replaces with placeholder when ratio still too high
- **Overflow detection** — monitors for context overflow errors, auto-triggers compaction (up to 3 attempts), falls back to tool result truncation

Key constants: `BASE_CHUNK_RATIO=0.4`, `MIN_CHUNK_RATIO=0.15`, `SAFETY_MARGIN=1.2`

Source files: `src/agents/compaction.ts`, `src/agents/pi-embedded-runner/compact.ts`, `src/agents/context-window-guard.ts`

## Proposed Approach

**Single-pass LLM summarization** triggered by a token-estimate threshold. When estimated context exceeds a configurable limit, the "middle" of the conversation (between the first user message and the most recent N messages) is summarized by Claude into a concise narrative and injected back as a context summary. The old messages are replaced, freeing context budget while preserving key facts.

This adapts openclaw's core idea while keeping the implementation proportional to this micro agent's scope.

## Architecture

### New File: `Compactor.cs`

A static helper class (matching the `LlmClient.cs` pattern) with two public methods:

```csharp
public static class Compactor
{
    /// Estimate total tokens for a message list using chars/4 heuristic.
    public static int EstimateTokens(IReadOnlyList<Message> messages);

    /// Summarize compactable messages via a Claude API call.
    /// Returns the summary text, count of compacted messages, and estimated tokens saved.
    public static async Task<(string Summary, int CompactedCount, int EstimatedTokensSaved)>
        CompactAsync(AnthropicClient client, string model,
                     List<Message> messages, int protectedTailMessages);
}
```

### Modified Files

| File | Change |
|------|--------|
| `AgentConfig.cs` | Add 3 fields: `CompactionThresholdTokens`, `ProtectedTailMessages`, `CompactionEnabled` |
| `Agent.cs` | Add `MaybeCompactAsync()`, `RebuildMessagesWithSummary()`; replace `TrimConversationHistory()` calls |
| `Program.cs` | Parse new config values from `appsettings.json` |
| `appsettings.json` | Add compaction settings |

### No Changes To

`LlmClient.cs`, `ITool.cs`, `SystemPrompt.cs`, any tool implementations. Compaction is entirely within the orchestration layer.

## Algorithm

### Token Estimation

Walk all `ContentBase` blocks in each message:

| Block Type | Character Source |
|------------|-----------------|
| `TextContent` | `.Text.Length` |
| `ToolUseContent` | `.Name.Length + .Input.ToJsonString().Length` |
| `ToolResultContent` | Sum of nested `TextContent.Text.Length` values |

Divide total characters by 4 to get estimated tokens. This is the standard heuristic for English text with the Claude tokenizer.

### Trigger Decision

Called at the same two points where `TrimConversationHistory()` currently runs (after adding a user message and after adding tool results):

```
estimated tokens > CompactionThresholdTokens (80K)?
  No  → return (do nothing)
  Yes → CompactionEnabled?
    No  → fall back to old TrimConversationHistory()
    Yes → compaction zone has ≥ 2 messages?
      No  → return (everything is protected, accept the large context)
      Yes → run compaction
```

### Message Protection

Three categories of messages are protected from compaction:

1. **Message[0]** (first user message) — always preserved. This establishes the task context. In the job search scenario, this is "Execute the job search prompt from search-prompt.txt".
2. **Last N messages** (`ProtectedTailMessages`, default 6) — always preserved. These are the most recent exchanges where active work is happening.
3. **Tool-use/result pairs** — the compaction boundary is adjusted so a `tool_use` block in an assistant message is never separated from its corresponding `tool_result` in the next user message.

Everything between index 1 and `messages.Count - ProtectedTailMessages` is the **compaction zone**.

### Boundary Adjustment

```csharp
// Ensure we don't split tool_use/tool_result pairs at the boundary
while (compactEnd > compactStart + 1)
{
    var boundary = messages[compactEnd];
    if (boundary.Role == RoleType.User &&
        boundary.Content?.OfType<ToolResultContent>().Any() == true)
    {
        compactEnd--;  // Pull boundary back to include the tool_use
        continue;
    }
    break;
}
```

### Summarization

1. **Convert** compactable messages to text with **tool result previews** — first 500 chars + last 200 chars of each tool result, not the full content. This dramatically reduces the input to the summarization call.

2. **Cap** summarization input at 100,000 characters. If exceeded, truncate from the middle with a marker.

3. **Call Claude** (non-streaming, Temperature=0, MaxTokens=4096) with a focused summarization prompt:

```
Summarize the following conversation history between a user and an AI assistant.
Preserve these details precisely:
- The original user request and any specific criteria or instructions
- All decisions made and their reasoning
- Key data points, URLs, file paths, and identifiers that may be needed later
- Any scores, rankings, or evaluations produced
- Current task status and next steps

Do NOT include raw tool output data (job descriptions, email bodies, etc.) —
just note what was retrieved and key findings.

Format as a concise narrative summary.
```

4. **Return** the summary text.

### Message Reconstruction

After compaction, the message list is rebuilt:

```
[0] User: original request + "\n\n[CONTEXT SUMMARY]\n{summary}\n[END CONTEXT SUMMARY]"
[1] Assistant: "Understood. Continuing with the current task."  (if needed for role alternation)
[2..N] Protected tail messages (unchanged)
```

The first user message and summary are merged into a single user message to avoid consecutive same-role messages. An assistant acknowledgment is inserted only if needed to maintain the strict role alternation required by the Anthropic API (i.e., if the first protected tail message is also a user-role message).

### Fallback

- If the summarization API call fails (network error, rate limit exhausted), catch the exception, log to stderr, and fall back to `TrimConversationHistory()`.
- The existing `MaxConversationMessages` trimming still runs as a hard backstop after compaction.

## Configuration

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `CompactionThresholdTokens` | int | 80,000 | Estimated token count that triggers compaction |
| `ProtectedTailMessages` | int | 6 | Recent messages to never compact (≈3 exchange pairs) |
| `CompactionEnabled` | bool | true | Kill switch to revert to old trimming behavior |

**Rationale for defaults:**

- **80,000 tokens** — Claude Sonnet/Haiku have a 200K context window. 80K leaves room for the system prompt (~500 tokens), tool definitions (~2K tokens), the response (~8K tokens), and a comfortable margin. This is the "start worrying" threshold.
- **6 protected tail messages** — Protects the most recent 3 exchange pairs (user + assistant). In the job search scenario, this keeps the current scoring/writing activity in context.

## Job Search Scenario Walkthrough

For "Execute the job search prompt from search-prompt.txt":

### Single Search Round (~25-30K tokens)

Generates ~8-10 messages. The tool results from `linkedin_job_detail` and `gmail_read` are the biggest consumers (~10K chars each). Total is well under the 80K threshold. **No compaction triggers.**

### After 2-3 Rounds (~85K tokens)

The user refines criteria and searches again. Tokens hit ~85K. Compaction triggers.

**Compaction zone**: Messages 1-18 (the first two search rounds).
**Protected zone**: Messages 19-24 (current scoring/writing activity) + Message 0 (original request).

**What the summary captures:**
> "The user requested a job search based on criteria in search-prompt.txt: .NET developer, senior level, remote, £500-700/day. First search found 10 LinkedIn results and 7 JobServe emails. Detailed analysis of 5 positions: [Company A - Senior .NET Dev - scored 7/10, URL: ...], [Company B - ...]. Gmail alerts yielded 3 additional positions. Scoring rubric: technical stack match (30%), remote policy (20%), salary (25%), growth (25%). Report file created: todays-jobs-2026-02-17.md."

**What is discarded** (but was preserved in the summary):
- Full job descriptions (10K chars each) — summarized as company/title/score/URL
- Full email bodies — summarized as key findings
- Intermediate "I'll search now..." assistant messages

**Result**: Context drops from ~85K to ~10K tokens. All key facts preserved. Claude can continue scoring, writing, or searching with full awareness of what was already done.

### Critical Preservation

| Content | Where After Compaction |
|---------|----------------------|
| Search criteria / scoring rubric | In message[0] (always protected as first user message) |
| Job scores and rankings | In the summary narrative |
| URLs and identifiers | In the summary narrative |
| Current task status | In protected tail messages |
| Full job descriptions | Discarded (can be re-fetched via tools if needed) |

## What This Omits vs OpenClaw

| OpenClaw Feature | Why Omitted |
|-----------------|-------------|
| Multi-stage chunked summarization | Single-pass is sufficient for our message volumes |
| Adaptive chunk sizing with token share | Unnecessary complexity for a micro agent |
| Context window guard with hard minimum | Simple threshold is enough |
| Compaction reserve calculation | Our summarization input is already small (previews, not full text) |
| Security stripping of toolResult.details | Our tool results are plain text, no separate `details` field |
| Overflow detection with retry loop | Polly retry + trimming fallback covers this |
| Separate context pruning phase | Existing `TruncateToolResult` handles per-result truncation |
| Plugin hooks (before/after compaction) | No plugin system in this project |

## Transparency

All compaction activity goes to stderr (matching the existing pattern):

```
  Compaction: estimated ~85,200 tokens, threshold 80,000 — compacting 18 messages
  Compaction: summarized 18 messages into ~800 tokens, freed ~72,000 estimated tokens
```

On failure:
```
  Warning: Compaction failed: {error}. Falling back to history trimming.
```

## Implementation Sequence

1. `AgentConfig.cs` — Add 3 new record parameters (smallest change, no dependencies)
2. `Compactor.cs` — Create new file with `EstimateTokens` and `CompactAsync` (can be tested independently)
3. `Agent.cs` — Add `MaybeCompactAsync()`, `RebuildMessagesWithSummary()`, update `RunAsync()` call sites
4. `Program.cs` — Wire up config parsing for 3 new settings
5. `appsettings.json` — Add default values
6. Update `DESIGN-agent-loop.md` — Add compaction section to the existing design doc
