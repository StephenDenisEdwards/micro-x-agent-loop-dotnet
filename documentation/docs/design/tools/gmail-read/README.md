# Tool: gmail_read

Read the full content of a Gmail email by its message ID.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `messageId` | string | Yes | The Gmail message ID (from `gmail_search` results) |

## Behavior

- Fetches the full message using the Gmail API
- Returns: From, To, Date, Subject, and the full email body
- For multipart messages, prefers HTML content over plain text
- Recursive MIME parsing handles nested multipart structures
- **Conditional registration:** Only available when Google credentials are configured

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Gmail/GmailReadTool.cs`
- Uses `GmailParser.ExtractText()` for recursive MIME body extraction
- Uses `GmailParser.GetHeader()` for case-insensitive header lookup
- Base64url decoding handled by `GmailParser`

## Example

```
you> Read the first email from those search results
```

Claude calls:
```json
{
  "name": "gmail_read",
  "input": {
    "messageId": "18e1a2b3c4d5e6f7"
  }
}
```

## Authentication

Same OAuth2 flow as other Gmail tools. See [gmail_search](../gmail-search/README.md) for authentication details.
