# Plan: Add `web_fetch` Tool

**Status: Completed** (2026-02-18)

## Context

The agent currently has no way to fetch web content. When tasks require reading articles, documentation, job listings, or API responses from URLs, the agent can't access them unless they arrive via Gmail or LinkedIn tools. Adding a general-purpose `web_fetch` tool unlocks a large class of use cases with minimal complexity.

This is Phase 1 of a broader web interaction roadmap (Phase 2: web search, Phase 3: browser automation). The design mirrors OpenClaw's `web_fetch` approach, adapted to this project's minimal patterns.

## Dependencies

**No new dependencies required.** The project already has:
- `HtmlAgilityPack` — HTML parsing (used by `HtmlUtilities.cs`)
- `HttpClient` — built-in async HTTP client

## New File

### `src/MicroXAgentLoop/Tools/Web/WebFetchTool.cs`

Single tool class following the existing `ITool` pattern (same as `LinkedInJobDetailTool`).

**Input schema:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `url` | string | yes | — | HTTP or HTTPS URL to fetch |
| `maxChars` | number | no | `50000` | Max characters to return (truncates with warning) |

**Behaviour:**

1. **Validate URL** — must be http(s), reject other schemes
2. **Fetch** — `HttpClient.GetAsync()` with browser-like User-Agent, 30s timeout, follow redirects (max 5)
3. **Extract content** based on Content-Type:
   - `text/html` → use existing `HtmlUtilities.HtmlToText()` for plain-text extraction (with preserved links)
   - `application/json` → pretty-print with `JsonSerializer`
   - Everything else → raw text
4. **Truncate** if content exceeds `maxChars`, append truncation notice
5. **Return** structured text with metadata header:
   ```
   URL: https://example.com/page
   Final URL: https://example.com/page (after redirects)
   Status: 200
   Content-Type: text/html
   Title: Page Title
   Length: 12,345 chars (truncated from 85,000)

   --- Content ---

   [extracted text]
   ```

**Error handling** (return error string, don't raise):
- Invalid URL → `"Error: URL must use http or https scheme"`
- Timeout → `"Error: Request timed out after 30 seconds"`
- HTTP errors → `"Error: HTTP {status_code} fetching {url}"`
- Network errors → `"Error: {exception description}"`

**Constants:**
- `DefaultMaxChars = 50_000`
- `MaxResponseBytes = 2_000_000` (2 MB, reject larger responses)
- `TimeoutSeconds = 30`
- `MaxRedirects = 5`

**Title extraction** — for HTML pages, pull `<title>` tag text before converting to plain text.

## Changes to Existing Files

### `src/MicroXAgentLoop/Tools/ToolRegistry.cs`
- Import `WebFetchTool`
- Add `WebFetchTool()` to the unconditional tools list (no credentials needed)

## Files Referenced (no changes needed)

| File | Reuse |
|------|-------|
| `src/MicroXAgentLoop/Tools/ITool.cs` | `ITool` interface — the interface to implement |
| `src/MicroXAgentLoop/Tools/HtmlUtilities.cs` | `HtmlToText()` for HTML→text extraction |
| `src/MicroXAgentLoop/Tools/LinkedIn/LinkedInJobDetailTool.cs` | Reference pattern for HTTP usage, User-Agent, error handling |

## Not in Scope (intentionally)

- **Caching** — keep it simple for now; add later if needed
- **Markdown extraction mode** — `HtmlToText()` already preserves links and structure; a markdown mode can be added later
- **Firecrawl fallback** — unnecessary complexity for Phase 1
- **SSRF protection** — the agent runs locally as a personal tool; not exposed to untrusted input
- **POST/PUT support** — GET only for now; extend later if needed

## Verification

1. **Basic fetch**: `web_fetch` with a public URL → returns page content with metadata header
2. **JSON API**: Fetch a JSON endpoint → returns pretty-printed JSON
3. **Truncation**: Fetch a large page with `maxChars: 500` → content truncated with notice
4. **Invalid URL**: Pass `ftp://example.com` → returns descriptive error
5. **Timeout/error**: Fetch non-existent domain → returns timeout/network error
6. **Redirect**: Fetch URL that redirects → final URL shown in metadata
7. **Tool registered**: Appears in startup banner tool list

## Future Phases

- **Phase 2: `web_search`** — Brave Search API integration for agent-driven research
- **Phase 3: `browser`** — Playwright-based browser automation for JS-heavy sites and form interaction
- **Phase 4: Session management** — Cookie persistence, login flows, browser profiles
