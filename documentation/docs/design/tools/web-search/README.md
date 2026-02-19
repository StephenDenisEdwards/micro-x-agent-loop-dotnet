# Tool: web_search

Search the web and return a list of results with titles, URLs, and descriptions.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | Search query (max 400 characters, silently truncated) |
| `count` | number | No | Number of results to return (1–20, default 5) |

## Behavior

- Delegates to an `ISearchProvider` implementation (currently `BraveSearchProvider`)
- Returns a numbered list of results with title, URL, and description
- Header line shows the query and result count
- **Conditional registration:** Only available when `BRAVE_API_KEY` is set in `.env`

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Web/WebSearchTool.cs`
- Uses `ISearchProvider` abstraction for pluggable search backends
- Current backend: `BraveSearchProvider` (Brave Web Search API via `HttpClientFactory.Api`)
- See [DESIGN-tool-system.md](../../DESIGN-tool-system.md) for details on `ISearchProvider` and adding new search backends

## Example

```
you> Search the web for "latest .NET 9 features" and give me the top 5 results
```
