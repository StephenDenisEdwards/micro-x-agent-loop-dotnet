# Design: Agent Loop

## Overview

The agent loop is the core runtime cycle of the application. It manages the streaming, tool-augmented conversation between the user, Claude (via the Anthropic API), and a set of registered tools.

Key files collaborate to implement the loop:

| File | Responsibility |
|------|----------------|
| `Program.cs` | REPL shell — reads user input, connects MCP, sets up logging, catches top-level exceptions |
| `Agent.cs` | Orchestrator — manages conversation history, dispatches tool calls, enforces safety limits, triggers compaction |
| `LlmClient.cs` | API client — streams responses via SSE, retries on rate limits with Polly |
| `LoggingConfig.cs` | Configures Serilog sinks (console + file) from settings |
| `Mcp/McpManager.cs` | Connects to MCP servers at startup, discovers tools |

## Flow

```
User types prompt
       │
       ▼
┌─────────────────────────────────┐
│  1. Add user message to history │
│  2. Maybe compact (summarize    │
│     old messages if over token  │
│     threshold)                  │
│  3. Trim history if over limit  │
│  4. Stream request to Claude    │◄──────────────────┐
│  5. Print text tokens to stdout │                    │
│     in real time                │                    │
└──────────┬──────────────────────┘                    │
           │                                           │
     ┌─────┴──────┐                                    │
     │ Tool calls? │──── No ──► Return to REPL         │
     └─────┬──────┘                                    │
           │ Yes                                       │
           ▼                                           │
┌──────────────────────────────┐                       │
│  6. Execute ALL tool calls   │                       │
│     in parallel              │                       │
│  7. Truncate oversized       │                       │
│     results                  │                       │
│  8. Add tool results to      │                       │
│     history                  │                       │
│  9. Maybe compact + trim     │                       │
└──────────┬───────────────────┘                       │
           │                                           │
           └───── Loop back ───────────────────────────┘
```

### Step-by-step walkthrough

1. **User input** — `Program.cs` reads a line from stdin and calls `agent.RunAsync(trimmed)`.
2. **Add to history** — The user message is wrapped in `Message(RoleType.User, ...)` and appended to `_messages`.
3. **Maybe compact** — `MaybeCompactAsync()` checks if estimated tokens exceed the threshold. If so, the `SummarizeCompactionStrategy` summarizes old messages into a concise narrative, preserving the first user message and the most recent N messages. See [Compaction Design](DESIGN-compaction.md) for details.
4. **Trim history** — If `_messages.Count` still exceeds `MaxConversationMessages` after compaction, the oldest messages are removed from the front. A warning is printed to stderr.
5. **Stream to Claude** — `LlmClient.StreamChatAsync` sends the full message list (system prompt + history + tool definitions) to the Anthropic API with `Stream = true`. Text deltas are written to stdout as they arrive via SSE, giving the user real-time feedback.
6. **Inspect response** — The assembled `Message` is added to history. If it contains no `ToolUseContent` blocks, the loop exits and control returns to the REPL.
7. **Execute tools** — If tool-use blocks are present, `ExecuteToolsAsync` runs all of them concurrently via `Task.WhenAll`. Each tool is looked up in `_toolMap` by name and called with the JSON input Claude provided. This includes both built-in tools and MCP tools (via `McpToolProxy`).
8. **Return results** — Tool results (or error messages) are wrapped in `ToolResultContent` and added to history as a user-role message (per the Anthropic API contract).
9. **Compact + repeat** — `MaybeCompactAsync()` runs again (tool results can be large), then the loop goes back to step 5.

This loop continues until Claude produces a response with no tool-use blocks, at which point the conversation turn is complete.

## Components

### Agent (`Agent.cs`)

The central orchestrator. It owns the mutable `_messages` list and coordinates the loop.

**Fields:**

| Field | Type | Purpose |
|-------|------|---------|
| `_client` | `AnthropicClient` | Anthropic API client (created once) |
| `_messages` | `List<Message>` | Conversation history (grows across turns) |
| `_toolMap` | `Dictionary<string, ITool>` | Name-to-tool lookup for dispatch |
| `_anthropicTools` | `List<CommonTool>` | Tool definitions in Anthropic SDK format |
| `_maxToolResultChars` | `int` | Truncation threshold for tool outputs |
| `_maxConversationMessages` | `int` | Maximum messages before trimming |

**Methods:**

- `RunAsync(string userMessage)` — Main entry point. Adds the user message, then loops: stream → check for tools → execute → repeat.
- `ExecuteToolsAsync(List<ToolUseContent>)` — Dispatches all tool calls in parallel, handles errors, truncates results.
- `TruncateToolResult(string, string)` — Cuts oversized output and appends a notice.
- `TrimConversationHistory()` — Removes oldest messages when the limit is exceeded.

### LlmClient (`LlmClient.cs`)

A static helper that encapsulates the Anthropic API call and retry logic.

**Key method — `StreamChatAsync`:**

```csharp
public static async Task<(Message Message, List<ToolUseContent> ToolUseBlocks)> StreamChatAsync(
    AnthropicClient client, string model, int maxTokens, decimal temperature,
    string systemPrompt, List<Message> messages, List<CommonTool> tools)
```

1. Builds `MessageParameters` with `Stream = true`.
2. Wraps the streaming call in a **Polly retry pipeline** (handles HTTP 429).
3. Iterates over SSE events: writes `res.Delta.Text` to stdout in real time.
4. After the stream completes, assembles the full `Message` and extracts `ToolUseContent` blocks.
5. Returns both to the Agent for processing.

**Polly retry configuration:**

| Setting | Value |
|---------|-------|
| Max retries | 5 |
| Backoff | Exponential, starting at 10 seconds |
| Trigger | `HttpRequestException` with `StatusCode == 429` |
| On retry | Logs attempt number and wait time to stderr |
| Total max wait | ~310 seconds |

If a partial stream is interrupted by a 429, the `outputs` list is cleared and the entire streaming call restarts from scratch. This is an acceptable trade-off — the alternative (resuming a partial stream) would add significant complexity.

### Program.cs (REPL)

The top-level shell that ties everything together:

1. Loads secrets from `.env` via DotNetEnv.
2. Loads settings from `appsettings.json` via Microsoft.Extensions.Configuration.
3. Validates that `ANTHROPIC_API_KEY` is set.
4. Builds the tool list via `ToolRegistry.GetAll()` (conditional Gmail registration).
5. Creates an `Agent` with an immutable `AgentConfig`.
6. Enters a `while (true)` REPL loop: `you> ` prompt → `agent.RunAsync()` → catch exceptions.

## Conversation History

Messages accumulate in `_messages` as the conversation progresses. The Anthropic Messages API requires strict role alternation, so the message sequence for a single tool-use turn looks like:

```
[User]      "Find jobs in Seattle"
[Assistant]  text: "I'll search..." + tool_use: linkedin_jobs({keyword: "engineer", location: "Seattle"})
[User]       tool_result: [{id: "...", content: "Found 5 jobs..."}]
[Assistant]  text: "Here are the results..."
```

Note that **tool results are sent as user-role messages** containing `ToolResultContent` — this is required by the Anthropic API contract.

When `_messages.Count` exceeds `MaxConversationMessages`, the oldest messages are removed from the front via `RemoveRange(0, removeCount)`. A warning is printed to stderr so the user knows context was lost.

## Parallel Tool Execution

When Claude requests multiple tools in a single turn, all are dispatched concurrently:

```csharp
var tasks = toolUseBlocks.Select(async block =>
{
    if (!_toolMap.TryGetValue(block.Name, out var tool))
        return ErrorResult("unknown tool");

    try
    {
        var result = await tool.ExecuteAsync(block.Input);
        result = TruncateToolResult(result, block.Name);
        return SuccessResult(result);
    }
    catch (Exception ex)
    {
        return ErrorResult(ex.Message);
    }
});

var results = await Task.WhenAll(tasks);
```

This is safe because:
- Tools are **stateless** — they don't share mutable state.
- Each tool operates on its own I/O (different files, different HTTP requests).
- `Task.WhenAll` preserves result order, matching the order Claude expects.

## Tool Result Truncation

Large tool outputs (e.g., reading a big file or a verbose bash command) can consume excessive context window tokens. When a result exceeds `MaxToolResultChars` (default: 40,000):

1. The result is cut at the character limit using a range expression: `result[.._maxToolResultChars]`
2. A notice is appended: `[OUTPUT TRUNCATED: Showing 40,000 of 120,000 characters from bash]`
3. A warning is printed to stderr for the user

This ensures Claude knows the output was truncated and can request a more targeted operation if needed (e.g., reading a specific line range instead of the whole file).

## Error Handling

Errors are handled at multiple levels to keep the loop running:

| Error | Where Handled | Strategy |
|-------|---------------|----------|
| Unknown tool name | `ExecuteToolsAsync` | Returns `ToolResultContent` with `IsError = true` — Claude sees the error and can adapt |
| Tool throws exception | `ExecuteToolsAsync` | Catches `Exception`, returns error message to Claude |
| API rate limit (HTTP 429) | `LlmClient` (Polly) | Exponential backoff retry, up to 5 attempts |
| Unrecoverable API error | `Program.cs` | Exception propagates to REPL catch block, user sees error, loop continues |
| Oversized tool output | `TruncateToolResult` | Truncated with appended notice |
| Oversized conversation | `TrimConversationHistory` | Oldest messages removed, warning to stderr |

The key principle is that **tool errors are fed back to Claude as data, not thrown as exceptions**. This allows Claude to reason about the error and try a different approach (e.g., correcting a file path or adjusting a command), rather than terminating the turn.

## Configuration

All loop-relevant settings flow through `AgentConfig`, an immutable record:

```csharp
public record AgentConfig(
    string Model,                          // e.g., "claude-sonnet-4-5-20250929"
    int MaxTokens,                         // Max response tokens (default: 8192)
    decimal Temperature,                   // Sampling temperature (default: 1.0)
    string ApiKey,                         // Anthropic API key
    IReadOnlyList<ITool> Tools,            // Registered tools (built-in + MCP)
    string SystemPrompt,                   // System prompt text
    int MaxToolResultChars = 40_000,       // Truncation limit per tool result
    int MaxConversationMessages = 50,      // History trimming limit
    ICompactionStrategy? CompactionStrategy = null);  // Context compaction
```

Settings are loaded from `appsettings.json` with code-level defaults as fallbacks. Secrets come from `.env`. See [appsettings.md](../operations/appsettings.md) for the full reference.

### Conversation Compaction

When `CompactionStrategy` is set to `"summarize"`, the `SummarizeCompactionStrategy` monitors estimated token usage and summarizes old messages when the threshold is exceeded. This preserves key facts (decisions, URLs, scores) while freeing context budget. The first user message and the most recent N messages are always protected. See [Compaction Design](DESIGN-compaction.md) for the full algorithm.

## Design Rationale

| Decision | Rationale |
|----------|-----------|
| **Always stream** | Real-time feedback is critical for UX. Users see tokens as they arrive rather than waiting for the full response. |
| **Parallel tool execution** | Multiple tool calls in one turn are independent by design. Running them concurrently reduces latency proportionally. |
| **Errors as data** | Returning errors to Claude (instead of throwing) lets the model self-correct. This is a core agentic pattern. |
| **History trimming from front** | Oldest messages are least relevant. Removing them preserves recent context where the active task lives. |
| **Truncation with notice** | Simply cutting output would confuse Claude. The appended message lets it know data was lost and adapt accordingly. |
| **Polly for retry** | Exponential backoff with jitter is the standard approach for rate-limited APIs. Polly provides a battle-tested implementation. |
| **`while (true)` loop** | The agent loop runs until Claude produces a response with no tool calls. This is the simplest possible control flow for an agentic loop. |
