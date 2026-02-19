# Tool: linkedin_job_detail

Fetch the full job description from a LinkedIn job URL.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `url` | string | Yes | The LinkedIn job URL (from a `linkedin_jobs` search result) |

## Behavior

- Fetches the full job page and extracts the description
- Returns: title, company, location, and the full job description text
- Uses `HtmlUtilities.HtmlToText()` to convert the HTML description to readable plain text with preserved link URLs

## Implementation

- Source: `src/MicroXAgentLoop/Tools/LinkedIn/LinkedInJobDetailTool.cs`
- Uses `HttpClient` for async HTTP requests
- Parses with `HtmlAgilityPack`
- Multiple XPath selector fallbacks for title, company, location, and description elements
- Uses shared `HtmlUtilities.HtmlToText()` for HTML conversion

## Example

```
you> Get the full description for that first job
```

Claude calls:
```json
{
  "name": "linkedin_job_detail",
  "input": {
    "url": "https://www.linkedin.com/jobs/view/1234567890"
  }
}
```

## Limitations

- LinkedIn may return different page layouts (A/B testing)
- IP-based blocking after many requests
- Some job pages require login to view full details
