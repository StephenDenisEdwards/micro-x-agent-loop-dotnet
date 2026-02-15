# Design: Tool System

## Overview

The tool system provides Claude with the ability to interact with the outside world. Each tool is a self-contained unit that accepts JSON input, performs an action, and returns a string result.

## ITool Interface

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonNode InputSchema { get; }
    Task<string> ExecuteAsync(JsonNode input);
}
```

| Member | Purpose |
|--------|---------|
| `Name` | Unique identifier sent to Claude (e.g., `"read_file"`) |
| `Description` | Natural-language description Claude uses to decide when to call the tool |
| `InputSchema` | JSON Schema defining the expected input parameters |
| `ExecuteAsync` | Executes the tool and returns a string result |

## Tool Registry

`ToolRegistry.GetAll()` assembles the tool list with their dependencies:

```csharp
public static IReadOnlyList<ITool> GetAll(
    string? documentsDirectory,
    string? googleClientId,
    string? googleClientSecret)
```

Gmail tools are **conditionally registered** — they are only added when both Google credentials are present. This prevents runtime errors when Gmail is not configured.

## Built-in Tools

### File System

| Tool | Description |
|------|-------------|
| `read_file` | Read text files and `.docx` documents. Resolves relative paths by walking up to the repo root, then falling back to the configured `DocumentsDirectory`. |
| `write_file` | Write content to a file, creating parent directories as needed. |

### Shell

| Tool | Description |
|------|-------------|
| `bash` | Execute a shell command (cmd.exe on Windows, bash on Unix). 30-second timeout with process tree killing. |

### LinkedIn

| Tool | Description |
|------|-------------|
| `linkedin_jobs` | Search LinkedIn job postings by keyword, location, date, job type, remote filter, experience level, and sort order. Scrapes the public jobs API. |
| `linkedin_job_detail` | Fetch the full job description from a LinkedIn job URL. |

### Gmail (conditional)

| Tool | Description |
|------|-------------|
| `gmail_search` | Search Gmail using Gmail search syntax (e.g., `is:unread`, `from:someone@example.com`). |
| `gmail_read` | Read the full content of a Gmail message by its ID. |
| `gmail_send` | Send a plain-text email. |

Gmail tools require OAuth2 authentication. On first use, a browser window opens for Google sign-in. Tokens are cached in `.gmail-tokens/`.

## Shared Utilities

### HtmlUtilities

`HtmlUtilities.HtmlToText(string html)` converts HTML to readable plain text. Used by both Gmail (email body parsing) and LinkedIn (job description extraction).

Handles:
- Block elements (p, div, h1-h6, blockquote, tr) with newlines
- List items with bullet markers
- Table cells with tab separation
- Script/style removal
- HTML entity decoding
- Whitespace normalization

### GmailParser

- `DecodeBody` — base64url decoding for Gmail message bodies
- `ExtractText` — recursive MIME parsing, prefers HTML over plain text for multipart/alternative
- `GetHeader` — case-insensitive header lookup

## Adding a New Tool

1. Create a class implementing `ITool` in the `Tools/` directory
2. Define `Name`, `Description`, and `InputSchema`
3. Implement `ExecuteAsync` with error handling (return error strings, don't throw)
4. Register it in `ToolRegistry.GetAll()`

Example skeleton:

```csharp
public class MyTool : ITool
{
    public string Name => "my_tool";
    public string Description => "Does something useful.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "param": {
                    "type": "string",
                    "description": "A required parameter"
                }
            },
            "required": ["param"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        var param = input["param"]!.GetValue<string>();
        try
        {
            // Do work
            return "Result";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
```
