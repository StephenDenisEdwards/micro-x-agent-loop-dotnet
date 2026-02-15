# ADR-003: Streaming Responses via SSE

## Status

Accepted

## Context

The original implementation used `GetClaudeMessageAsync`, which waits for the complete response before displaying anything to the user. For longer responses, this creates a noticeable delay with no feedback — the user stares at a blank prompt for several seconds.

The Anthropic API supports Server-Sent Events (SSE) streaming via `StreamClaudeMessageAsync`, which returns an `IAsyncEnumerable<MessageResponse>` with incremental text deltas.

## Decision

Replace `GetClaudeMessageAsync` with `StreamClaudeMessageAsync`. Text deltas are printed to stdout as they arrive. Tool use blocks are collected from the streamed responses and dispatched after the stream completes.

The streaming call is wrapped in the existing Polly retry pipeline. On retry, `outputs` is cleared and the stream restarts.

## Consequences

**Easier:**
- Much better user experience — text appears word-by-word as Claude generates it
- User can see partial responses and interrupt early if the agent is going in the wrong direction
- Same tool dispatch logic works — tool blocks are collected after streaming ends

**Harder:**
- Response text is printed directly to stdout during streaming, so `Agent.RunAsync` no longer returns a string
- Program.cs must print the `assistant>` prefix before calling `RunAsync`
- Retry on rate limit restarts the entire stream (acceptable trade-off)
- Harder to unit test (would need to mock `IAsyncEnumerable`)
