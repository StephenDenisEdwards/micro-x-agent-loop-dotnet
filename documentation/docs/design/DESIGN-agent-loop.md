# Design: Agent Loop

## Overview

The agent loop is the core runtime cycle of the application. It manages the conversation between the user, Claude, and the tool system.

## Flow

1. User types a prompt
2. Prompt is added to conversation history
3. Conversation history is trimmed if over the limit
4. Message list is sent to Claude via streaming API
5. Text deltas are printed to stdout as they arrive
6. If Claude requests tool use:
   a. All tool calls execute **in parallel** via `Task.WhenAll`
   b. Results are truncated if over the character limit
   c. Tool results are added to conversation history
   d. Loop back to step 3
7. If Claude returns a final text response, control returns to the REPL

## Components

### Agent

`Agent.cs` orchestrates the loop. Key responsibilities:

- Maintains the `_messages` list (conversation history)
- Calls `LlmClient.StreamChatAsync` for each turn
- Dispatches tool calls in parallel via `ExecuteToolsAsync`
- Enforces `MaxToolResultChars` truncation
- Enforces `MaxConversationMessages` trimming

### LlmClient

`LlmClient.cs` handles the Anthropic API interaction:

- `StreamChatAsync` — streams the response, printing text deltas in real time
- Polly retry pipeline wraps the streaming call for rate limit resilience
- Returns a tuple of `(Message, List<ToolUseContent>)` for the agent to process

### Conversation History

Messages accumulate in `_messages` as the conversation progresses. Each message is either:

- **User message** — the user's text input
- **Assistant message** — Claude's response (text + tool_use blocks)
- **Tool result message** — tool execution results (role: user, content: ToolResultContent)

When `_messages.Count` exceeds `MaxConversationMessages`, the oldest messages are removed from the front. A warning is printed to stderr so the user knows context was lost.

## Parallel Tool Execution

When Claude requests multiple tools in a single turn, they execute concurrently:

```csharp
var tasks = toolUseBlocks.Select(async block => {
    var result = await tool.ExecuteAsync(block.Input);
    return new ToolResultContent { ... };
});
var results = await Task.WhenAll(tasks);
```

This is safe because tools are stateless and independent. The results are returned in the same order as the requests.

## Tool Result Truncation

Large tool outputs (e.g., reading a big file) can consume excessive tokens. When a result exceeds `MaxToolResultChars`:

1. The result is cut at the character limit
2. A clear message is appended: `[OUTPUT TRUNCATED: Showing X of Y characters from tool_name]`
3. A warning is printed to stderr

This ensures Claude knows the output was truncated and can request a more targeted read if needed.

## Error Handling

| Error | Handling |
|-------|----------|
| Unknown tool name | Error result returned to Claude |
| Tool throws exception | Error message returned to Claude |
| API rate limit (429) | Polly retries with exponential backoff |
| Unrecoverable API error | Exception propagates to REPL, user sees error |
