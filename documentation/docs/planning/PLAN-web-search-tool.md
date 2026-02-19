# Plan: Add `web_search` Tool (Phase 2)

**Status: Completed**

## Context

With `web_fetch` (Phase 1) complete, the agent can read known URLs. However, it cannot discover URLs on its own. A `web_search` tool enables the agent to research topics, find documentation, locate job postings, and answer questions requiring up-to-date information.

## Approach

Integrate a search API that returns a list of results (title, URL, snippet). The agent can then use `web_fetch` to read the most relevant results.

### API Options

| Option | Pros | Cons |
|--------|------|------|
| **Brave Search API** | Generous free tier (2k queries/mo), good quality, privacy-focused | Requires API key |
| **SerpAPI** | Google-quality results, structured data | Paid, heavier dependency |
| **DuckDuckGo** (via library) | No API key needed, free | Unofficial, may break, rate-limited |

**Recommended:** Brave Search API — good balance of quality, cost, and reliability.

## Input Schema

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `query` | string | yes | — | Search query |
| `count` | number | no | `10` | Number of results to return (max 20) |

## Output Format

```
Search: "dotnet async tutorial"
Results: 10

1. Microsoft Learn — Asynchronous programming in C#
   https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/
   A comprehensive guide to async/await in C# covering Task, async methods...

2. ...
```

## Implementation

- New file: `src/MicroXAgentLoop/Tools/Web/WebSearchTool.cs`
- New file: `src/MicroXAgentLoop/Tools/Web/BraveSearchProvider.cs`
- Credential: `BRAVE_API_KEY` in `.env` (conditionally registered like Gmail tools)
- Register in `ToolRegistry.cs` behind API key check
- Use `HttpClient` for the API call (built-in)

## Not in Scope

- Multi-engine fallback — keep it simple with one provider
- Result caching — add later if needed
- Image/video/news search — text results only for now

## Dependencies

- Brave Search API key (free tier: https://brave.com/search/api/)
- No new NuGet packages needed (`HttpClient` is built-in)
