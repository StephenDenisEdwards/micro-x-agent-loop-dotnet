# Tool: web_fetch

Fetch content from a URL and return it as readable text.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `url` | string | Yes | The URL to fetch |
| `maxChars` | number | No | Maximum characters to return (default 50,000) |

## Behavior

- Makes a GET request to the URL using `HttpClientFactory.Browser` (browser-like headers, redirect following)
- Detects content type and processes accordingly:
  - **HTML** — converted to plain text via `HtmlUtilities.HtmlToText()`, page title extracted
  - **JSON** — pretty-printed with `JsonSerializer`
  - **Plain text** — returned as-is
- Returns a metadata header (URL, final URL if redirected, HTTP status, content-type, page title, content length) followed by a `--- Content ---` separator and the extracted text
- Content exceeding `maxChars` is truncated with a notice appended
- **Always registered** — no API key required

## Limits

| Limit | Value |
|-------|-------|
| Max response size | 2 MB |
| Default max chars returned | 50,000 |
| Timeout | 30 seconds |
| Max redirects | 5 |

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Web/WebFetchTool.cs`
- Uses `HttpClientFactory.Browser` for HTTP requests (shared with LinkedIn tools)
- Uses `HtmlUtilities.HtmlToText()` for HTML-to-text conversion

## Example

```
you> Fetch the content of https://example.com and summarise it
```
