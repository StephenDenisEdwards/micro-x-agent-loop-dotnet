# Design: Tool System

## Overview

The tool system provides Claude with the ability to interact with the outside world. Each tool is a self-contained unit that accepts JSON input, performs an action, and returns a string result. Tools are divided into built-in tools (always registered), conditional tools (registered when credentials are present), and MCP tools (discovered at runtime from external servers).

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
    string? workingDirectory = null,
    string? googleClientId = null,
    string? googleClientSecret = null,
    string? anthropicAdminApiKey = null,
    string? braveApiKey = null)
```

| Parameter | Source | Controls |
|-----------|--------|----------|
| `workingDirectory` | `config.json` | Base path for `bash`, `read_file`, `write_file`, `append_file` |
| `googleClientId` | `.env` `GOOGLE_CLIENT_ID` | Gmail and Calendar tools |
| `googleClientSecret` | `.env` `GOOGLE_CLIENT_SECRET` | Gmail and Calendar tools |
| `anthropicAdminApiKey` | `.env` `ANTHROPIC_ADMIN_API_KEY` | `anthropic_usage` tool |
| `braveApiKey` | `.env` `BRAVE_API_KEY` | `web_search` tool |

**Registration groups:**

1. **Always registered** -- `bash`, `read_file`, `write_file`, `append_file`, `linkedin_jobs`, `linkedin_job_detail`, `web_fetch`
2. **Conditional on Google credentials** -- `gmail_search`, `gmail_read`, `gmail_send`, `calendar_list_events`, `calendar_create_event`, `calendar_get_event`
3. **Conditional on Anthropic Admin API key** -- `anthropic_usage`
4. **Conditional on Brave API key** -- `web_search`
5. **MCP tools** -- discovered dynamically via `McpManager.ConnectAllAsync()` and appended to the tool list at startup

## Built-in Tools

### File System

| Tool | Class | Description |
|------|-------|-------------|
| `read_file` | `ReadFileTool` | Read text files and `.docx` documents. Resolves relative paths by walking up to the repo root, then falling back to the configured `WorkingDirectory`. |
| `write_file` | `WriteFileTool` | Write content to a file, creating parent directories as needed. Resolves relative paths against `WorkingDirectory`. |
| `append_file` | `AppendFileTool` | Append content to an existing file. The file must already exist -- use `write_file` to create it first. Resolves relative paths against `WorkingDirectory`. |

All file-system tools accept a `workingDirectory` constructor parameter. Relative paths are resolved against this directory when the path is not rooted.

### Shell

| Tool | Class | Description |
|------|-------|-------------|
| `bash` | `BashTool` | Execute a shell command (`cmd.exe` on Windows, `bash` on Unix). 30-second timeout with process tree killing. Working directory set to the configured `WorkingDirectory`. |

### LinkedIn

| Tool | Class | Description |
|------|-------|-------------|
| `linkedin_jobs` | `LinkedInJobsTool` | Search LinkedIn job postings by keyword, location, date, job type, remote filter, experience level, and sort order. Scrapes the public jobs API. |
| `linkedin_job_detail` | `LinkedInJobDetailTool` | Fetch the full job description from a LinkedIn job URL. |

### Web Tools

| Tool | Class | Description |
|------|-------|-------------|
| `web_fetch` | `WebFetchTool` | Fetch content from a URL and return it as readable text. Supports HTML (converted to plain text via `HtmlUtilities`), JSON (pretty-printed), and plain text. GET requests only. |
| `web_search` | `WebSearchTool` | Search the web and return a list of results with titles, URLs, and descriptions. Delegates to an `ISearchProvider` implementation. Conditional on `BRAVE_API_KEY`. |

#### WebFetchTool details

- **Max response size:** 2 MB
- **Default max chars returned:** 50,000 (configurable via `maxChars` input parameter)
- **Timeout:** 30 seconds
- **Redirect handling:** Up to 5 automatic redirects
- **User-Agent:** Chrome-like browser string to avoid bot blocks
- **Output format:** Metadata header (URL, final URL if redirected, HTTP status, content-type, page title for HTML, content length) followed by `--- Content ---` separator and the extracted text
- **Truncation:** Content exceeding `maxChars` is truncated with a notice appended

#### ISearchProvider and BraveSearchProvider

`ISearchProvider` is the abstraction for pluggable web search backends:

```csharp
public record SearchResult(string Title, string Url, string Description);

public interface ISearchProvider
{
    string ProviderName { get; }
    Task<List<SearchResult>> SearchAsync(string query, int count);
}
```

`BraveSearchProvider` implements `ISearchProvider` using the Brave Web Search API (`https://api.search.brave.com/res/v1/web/search`). It authenticates via the `X-Subscription-Token` header with the configured API key. 30-second timeout.

#### WebSearchTool details

- **Query max length:** 400 characters (silently truncated)
- **Result count:** 1--20 (default 5, clamped)
- **Output format:** Header line with query and count, then numbered results with title, URL, and description

### Gmail (conditional on Google credentials)

| Tool | Class | Description |
|------|-------|-------------|
| `gmail_search` | `GmailSearchTool` | Search Gmail using Gmail search syntax (e.g., `is:unread`, `from:someone@example.com`). |
| `gmail_read` | `GmailReadTool` | Read the full content of a Gmail message by its ID. |
| `gmail_send` | `GmailSendTool` | Send a plain-text email. |

Gmail tools require OAuth2 authentication. On first use, a browser window opens for Google sign-in. Tokens are cached in `.gmail-tokens/`.

### Google Calendar (conditional on Google credentials)

| Tool | Class | Description |
|------|-------|-------------|
| `calendar_list_events` | `CalendarListEventsTool` | List Google Calendar events by date range or search query. Returns event ID, summary, start/end times, location, status, and organizer. Defaults to today's events if no time range is specified. |
| `calendar_create_event` | `CalendarCreateEventTool` | Create a Google Calendar event. Supports timed events (ISO 8601 with time) and all-day events (YYYY-MM-DD date only). Can add attendees by email. |
| `calendar_get_event` | `CalendarGetEventTool` | Get full details of a Google Calendar event by its event ID, including attendees, conference links, and recurrence rules. |

Calendar tools share the same OAuth2 flow and credential requirements as Gmail tools. Tokens are cached in `.calendar-tokens/`.

### Anthropic Tools (conditional on Anthropic Admin API key)

| Tool | Class | Description |
|------|-------|-------------|
| `anthropic_usage` | `AnthropicUsageTool` | Query the Anthropic Admin API for organization usage and cost reports. |

#### AnthropicUsageTool details

- **Base URL:** `https://api.anthropic.com/v1/organizations`
- **Authentication:** `x-api-key` header with the configured Admin API key; `anthropic-version: 2023-06-01`
- **Actions:**

| Action | Endpoint | Description |
|--------|----------|-------------|
| `usage` | `/usage_report/messages` | Token-level usage report. Supports `1m`, `1h`, `1d` bucket widths. Group by `model`, `workspace_id`, etc. |
| `cost` | `/cost_report` | Spend report. Amounts are converted from cents to USD (`amount` field replaced with `amount_usd`). Only supports `1d` bucket width. Group by `workspace_id`, `description`. |
| `claude_code` | `/usage_report/claude_code` | Claude Code productivity metrics. Date format is `YYYY-MM-DD`. |

- **Required parameters:** `action`, `starting_at`
- **Optional parameters:** `ending_at`, `bucket_width`, `group_by` (array), `limit`

## MCP Integration

The Model Context Protocol (MCP) integration allows the agent to dynamically discover and use tools from external MCP servers at startup.

### Architecture

```
config.json "mcpServers" section
        |
        v
   McpManager          -- manages connections to all configured servers
        |
        v
   McpClient           -- per-server connection (stdio or http transport)
        |
        v
   McpToolProxy        -- adapts each MCP tool to the ITool interface
```

### McpManager

`McpManager` manages connections to all configured MCP servers:

```csharp
public class McpManager : IAsyncDisposable
{
    public McpManager(Dictionary<string, McpServerConfig> serverConfigs);
    public async Task<List<ITool>> ConnectAllAsync();
    public async ValueTask DisposeAsync();
}
```

- **Connects to all servers** listed in `config.json` under the `"mcpServers"` key
- **Supported transports:** `stdio` (spawns a child process with `Command` and `Args`) and `http` (connects to a `Url` endpoint)
- **Fault isolation:** Individual server connection failures are logged but do not block startup or prevent other servers from connecting
- **Cleanup:** Implements `IAsyncDisposable` to shut down all client connections

### McpServerConfig

```csharp
public class McpServerConfig
{
    public string? Transport { get; set; }  // "stdio" (default) or "http"
    public string? Command { get; set; }    // For stdio: executable to spawn
    public string[]? Args { get; set; }     // For stdio: command-line arguments
    public Dictionary<string, string>? Env { get; set; }  // Environment variables
    public string? Url { get; set; }        // For http: server endpoint URL
}
```

### McpToolProxy

`McpToolProxy` adapts an MCP tool definition into the `ITool` interface:

```csharp
public class McpToolProxy : ITool
{
    public McpToolProxy(string serverName, McpClientTool mcpTool, McpClient client);
}
```

| Aspect | Behavior |
|--------|----------|
| **Name** | `{serverName}__{toolName}` (double underscore separator) |
| **Description** | Taken directly from the MCP tool definition |
| **InputSchema** | Parsed from the MCP tool's JSON schema |
| **ExecuteAsync** | Converts `JsonNode` input to a dictionary, calls `client.CallToolAsync`, extracts text content blocks from the result |
| **Error handling** | If the MCP result has `IsError == true`, throws `InvalidOperationException` with the text content |

### MCP tool naming

MCP tools are namespaced to avoid collisions with built-in tools and tools from other MCP servers. The naming convention is `{serverName}__{toolName}`. For example, a tool named `send_message` on a server named `whatsapp` becomes `whatsapp__send_message`.

## Shared Utilities

### HtmlUtilities

`HtmlUtilities.HtmlToText(string html)` converts HTML to readable plain text. Used by Gmail (email body parsing), LinkedIn (job description extraction), and WebFetchTool (HTML page content extraction).

Handles:
- Block elements (p, div, h1-h6, blockquote, tr) with newlines
- List items with bullet markers
- Table cells with tab separation
- Script/style removal
- HTML entity decoding
- Whitespace normalization

### GmailParser

- `DecodeBody` -- base64url decoding for Gmail message bodies
- `ExtractText` -- recursive MIME parsing, prefers HTML over plain text for multipart/alternative
- `GetHeader` -- case-insensitive header lookup

## Adding a New Tool

1. Create a class implementing `ITool` in the appropriate `Tools/` subdirectory
2. Define `Name`, `Description`, and `InputSchema`
3. Implement `ExecuteAsync` with error handling (return error strings, don't throw)
4. Register it in `ToolRegistry.GetAll()` -- unconditionally or behind a credential check

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

### Adding a new search provider

To add a search backend other than Brave, implement `ISearchProvider`:

```csharp
public class MySearchProvider : ISearchProvider
{
    public string ProviderName => "MySearch";

    public async Task<List<SearchResult>> SearchAsync(string query, int count)
    {
        // Call your search API and return results
    }
}
```

Then wire it up in `ToolRegistry.GetAll()` behind the appropriate API key check, passing it to `new WebSearchTool(new MySearchProvider(apiKey))`.
