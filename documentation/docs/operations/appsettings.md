# Configuration Reference

Configuration is split into two files:

- **`.env`** — secrets (API keys), loaded by DotNetEnv
- **`appsettings.json`** — application settings, loaded by Microsoft.Extensions.Configuration

## Secrets (`.env`)

| Variable | Required | Description |
|----------|----------|-------------|
| `ANTHROPIC_API_KEY` | Yes | Anthropic API key for Claude |
| `GOOGLE_CLIENT_ID` | No | Google OAuth client ID for Gmail, Calendar, and Contacts tools |
| `GOOGLE_CLIENT_SECRET` | No | Google OAuth client secret for Gmail, Calendar, and Contacts tools |
| `ANTHROPIC_ADMIN_API_KEY` | No | Anthropic Admin API key for usage reports |
| `BRAVE_API_KEY` | No | Brave Search API key for web search |

If `GOOGLE_CLIENT_ID` or `GOOGLE_CLIENT_SECRET` is missing, Gmail, Calendar, and Contacts tools are not registered. If `ANTHROPIC_ADMIN_API_KEY` is missing, the `anthropic_usage` tool is not registered. If `BRAVE_API_KEY` is missing, the `web_search` tool is not registered. All other tools work normally.

## App Settings (`appsettings.json`)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Model` | string | `"claude-sonnet-4-5-20250929"` | Claude model ID to use |
| `MaxTokens` | int | `8192` | Maximum tokens per API response |
| `Temperature` | decimal | `1.0` | Sampling temperature (0.0 = deterministic, 1.0 = creative) |
| `MaxToolResultChars` | int | `40000` | Maximum characters per tool result before truncation |
| `MaxConversationMessages` | int | `50` | Maximum messages in conversation history before trimming |
| `WorkingDirectory` | string | _(none)_ | Working directory for tools (bash cwd, file path resolution) |
| `CompactionStrategy` | string | `"none"` | Context compaction strategy: `"none"` or `"summarize"` |
| `CompactionThresholdTokens` | int | `80000` | Estimated token count that triggers compaction |
| `ProtectedTailMessages` | int | `6` | Number of recent messages to preserve during compaction |
| `LogLevel` | string | `"Information"` | Minimum log level (`Verbose`/`Debug`/`Information`/`Warning`/`Error`/`Fatal`) |
| `LogConsumers` | array | console + file | Logging sinks configuration (see below) |
| `McpServers` | object | _(none)_ | MCP server configurations (see below) |

### Example

```json
{
  "Model": "claude-sonnet-4-5-20250929",
  "MaxTokens": 32768,
  "Temperature": 1.0,
  "MaxToolResultChars": 40000,
  "MaxConversationMessages": 50,
  "WorkingDirectory": "C:\\Users\\you\\documents",
  "CompactionStrategy": "summarize",
  "CompactionThresholdTokens": 80000,
  "ProtectedTailMessages": 6,
  "LogLevel": "Debug",
  "LogConsumers": [
    { "type": "console" },
    { "type": "file", "path": "agent.log" }
  ],
  "McpServers": {
    "system-info": {
      "transport": "stdio",
      "command": "dotnet",
      "args": [ "run", "--no-build", "--project", "mcp-servers/system-info" ]
    },
    "remote-service": {
      "transport": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

All settings are optional — sensible defaults are used when a setting is missing.

## Setting Details

### Model

The Anthropic model ID. Common values:

| Model | Description |
|-------|-------------|
| `claude-sonnet-4-5-20250929` | Good balance of capability and cost |
| `claude-opus-4-6` | Most capable, higher cost |
| `claude-haiku-4-5-20251001` | Fastest, lowest cost |

### MaxTokens

Controls the maximum length of Claude's response. Higher values allow longer responses but use more of your rate limit budget.

### Temperature

Controls randomness in Claude's responses:
- `0.0` — most deterministic, best for factual/precise tasks
- `1.0` — default, good general-purpose balance
- Values above 1.0 increase randomness

### MaxToolResultChars

When a tool returns more than this many characters, the output is truncated and a message is appended:

```
[OUTPUT TRUNCATED: Showing 40,000 of 85,000 characters from read_file]
```

A warning is also printed to stderr. Set to `0` to disable truncation.

### MaxConversationMessages

When the conversation history exceeds this count, the oldest messages are removed. A note is printed to stderr:

```
Note: Conversation history trimmed — removed 2 oldest message(s) to stay within the 50 message limit
```

This helps prevent rate limit errors from growing context. Set to `0` to disable trimming.

### WorkingDirectory

Sets the working directory for tool execution:
- **`bash`** — uses this as the current working directory for shell commands
- **`read_file` / `write_file` / `append_file`** — resolves relative file paths against this directory

Use an absolute path for reliability. When not set, tools use the process working directory.

### CompactionStrategy

Controls how the agent manages long conversation context:

| Value | Behavior |
|-------|----------|
| `"none"` | No compaction. Conversation grows until `MaxConversationMessages` trims it. |
| `"summarize"` | When estimated tokens exceed the threshold, older messages are summarized into a compact narrative and replaced inline. Recent messages (the protected tail) are preserved verbatim. |

When compaction triggers, progress messages are written to stderr:

```
  Compaction: estimated ~95,000 tokens, threshold 80,000 — compacting 12 messages
  Compaction: summarized 12 messages into ~1,200 tokens, freed ~45,000 estimated tokens
```

### CompactionThresholdTokens

The estimated token count that triggers compaction when `CompactionStrategy` is `"summarize"`. Token count is estimated at roughly 4 characters per token. The default of `80000` works well for most models and rate-limit tiers.

### ProtectedTailMessages

The number of most-recent messages to preserve verbatim during compaction. These messages are never summarized, ensuring the agent retains full context of the latest interaction. The default of `6` typically covers the last user request and its tool calls.

### LogLevel

Sets the minimum severity for log output. Messages below this level are discarded.

| Level | Description |
|-------|-------------|
| `Verbose` | Extremely detailed tracing (alias: `Trace`) |
| `Debug` | Internal diagnostic details |
| `Information` | General operational events (default) |
| `Warning` | Unexpected situations that don't prevent operation (alias: `Warn`) |
| `Error` | Failures that affect the current operation |
| `Fatal` | Application-stopping failures (alias: `Critical`) |

### LogConsumers

An array of logging sink configurations. Each entry has a `type` and optional sink-specific settings.

When `LogConsumers` is omitted entirely, the default is both console and file sinks at the configured `LogLevel`.

#### Console sink

Writes log output to stderr (so it doesn't interfere with the agent's stdout conversation).

```json
{ "type": "console" }
```

Optional: override the level for this sink only:

```json
{ "type": "console", "level": "Warning" }
```

#### File sink

Writes log output to a file with automatic rotation (10 MB size limit, 3 retained files).

```json
{ "type": "file", "path": "agent.log" }
```

Optional: override the level for this sink only:

```json
{ "type": "file", "path": "logs/debug.log", "level": "Debug" }
```

If the directory in `path` does not exist, it is created automatically.

### McpServers

A dictionary of MCP (Model Context Protocol) server configurations. Each key is a server name used as a prefix for discovered tools (e.g., `system-info__get_os`).

#### Stdio transport

Launches a local process and communicates over stdin/stdout:

```json
{
  "McpServers": {
    "my-server": {
      "transport": "stdio",
      "command": "dotnet",
      "args": [ "run", "--no-build", "--project", "path/to/server" ]
    }
  }
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `transport` | Yes | Must be `"stdio"` |
| `command` | Yes | Executable to launch |
| `args` | No | Command-line arguments array |
| `env` | No | Environment variables object |

#### HTTP transport

Connects to a remote MCP server over HTTP:

```json
{
  "McpServers": {
    "remote-server": {
      "transport": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `transport` | Yes | Must be `"http"` |
| `url` | Yes | MCP server endpoint URL |

#### Environment variables

For stdio servers, you can pass environment variables:

```json
{
  "McpServers": {
    "whatsapp": {
      "transport": "stdio",
      "command": "npx",
      "args": [ "@anthropic/whatsapp-mcp" ],
      "env": {
        "WHATSAPP_TOKEN": "your-token-here"
      }
    }
  }
}
```

At startup, discovered MCP tools are listed under their server name:

```
MCP servers:
  - system-info: get_os, get_memory
  - whatsapp: send_message, list_chats
```
