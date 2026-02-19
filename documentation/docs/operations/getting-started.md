# Getting Started

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- An [Anthropic API key](https://console.anthropic.com/)
- (Optional) Google OAuth credentials for Gmail/Calendar tools
- (Optional) [Brave Search API key](https://brave.com/search/api/) for web search
- (Optional) [Anthropic Admin API key](https://console.anthropic.com/) for usage reports

## Setup

### 1. Clone the repository

```bash
git clone https://github.com/StephenDenisEdwards/micro-x-agent-loop-dotnet.git
cd micro-x-agent-loop-dotnet
```

### 2. Create the `.env` file

Create `src/MicroXAgentLoop/.env` with your API keys:

```
ANTHROPIC_API_KEY=sk-ant-your-key-here
GOOGLE_CLIENT_ID=your-client-id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=your-client-secret
ANTHROPIC_ADMIN_API_KEY=your-admin-key-here
BRAVE_API_KEY=your-brave-api-key-here
```

Only `ANTHROPIC_API_KEY` is required. The other keys are optional:
- **Google credentials** — if omitted, Gmail and Calendar tools are not registered
- **`ANTHROPIC_ADMIN_API_KEY`** — if omitted, the `anthropic_usage` tool is not registered
- **`BRAVE_API_KEY`** — if omitted, the `web_search` tool is not registered

All other tools work normally regardless of which optional keys are present.

### 3. Configure app settings

Edit `src/MicroXAgentLoop/appsettings.json` to set your preferences:

```json
{
  "Model": "claude-sonnet-4-5-20250929",
  "MaxTokens": 8192,
  "WorkingDirectory": "C:\\path\\to\\your\\documents",
  "CompactionStrategy": "summarize",
  "LogLevel": "Information"
}
```

See [Configuration Reference](appsettings.md) for all available settings.

### 4. Configure MCP servers (optional)

Add MCP server configurations to `appsettings.json` to extend the agent with external tools:

```json
{
  "McpServers": {
    "system-info": {
      "transport": "stdio",
      "command": "dotnet",
      "args": [ "run", "--no-build", "--project", "mcp-servers/system-info" ]
    }
  }
}
```

MCP servers are connected at startup and their tools are automatically discovered and registered. See [Configuration Reference](appsettings.md) for stdio and HTTP transport options.

### 5. Build and run

```bash
cd src/MicroXAgentLoop
dotnet run
```

You should see:

```
micro-x-agent-loop (type 'exit' to quit)
Tools:
  - bash
  - read_file
  - write_file
  - append_file
  - linkedin_jobs
  - linkedin_job_detail
  - web_fetch
  - gmail_search
  - gmail_read
  - gmail_send
  - calendar_list_events
  - calendar_create_event
  - calendar_get_event
  - anthropic_usage
  - web_search
MCP servers:
  - system-info: get_os, get_memory
Working directory: C:\path\to\your\documents
Compaction: summarize (threshold: 80,000 tokens, tail: 6 messages)
Logging: console (stderr, Information), file (agent.log, Information)

you>
```

The startup output is shown line-by-line:
- **Tools** — all built-in tools, listed individually. Tools that depend on optional API keys only appear when the corresponding key is set in `.env`.
- **MCP servers** — each configured MCP server and its discovered tools. Only shown when `McpServers` is configured.
- **Working directory** — only shown when `WorkingDirectory` is set in `appsettings.json`.
- **Compaction** — only shown when `CompactionStrategy` is not `"none"`. Displays the strategy, token threshold, and protected tail message count.
- **Logging** — lists each active logging sink and its level. Only shown when sinks are configured.

## First Use

Try a simple prompt to verify everything works:

```
you> What files are in the current directory?
```

The agent will use the `bash` tool to run `dir` or `ls` and report the results.

### Gmail / Calendar First Use

The first time you use a Gmail or Calendar tool, a browser window will open for Google OAuth sign-in. After authorizing, tokens are cached in `.gmail-tokens/` for future sessions.

```
you> Search my Gmail for unread emails from the last 3 days
```

### Web Search First Use

If `BRAVE_API_KEY` is configured, you can search the web:

```
you> Search the web for the latest .NET 8 release notes
```

### Anthropic Usage First Use

If `ANTHROPIC_ADMIN_API_KEY` is configured, you can query your organization's API usage:

```
you> Show me my Anthropic API usage for the last 7 days
```

## Build Commands

| Command | Description |
|---------|-------------|
| `dotnet build` | Build the project |
| `dotnet run` | Build and run |
| `dotnet run --project src/MicroXAgentLoop` | Run from repo root |

## Project Structure

```
micro-x-agent-loop-dotnet/
├── MicroXAgentLoop.slnx              # Solution file
├── README.md                          # Project overview
├── documentation/docs/                # Full documentation
└── src/MicroXAgentLoop/
    ├── .env                           # Secrets (not in git)
    ├── appsettings.json               # App configuration
    ├── Program.cs                     # Entry point and REPL
    ├── Agent.cs                       # Agent loop orchestrator
    ├── AgentConfig.cs                 # Configuration record
    ├── LlmClient.cs                  # Anthropic API + streaming + Polly
    ├── SystemPrompt.cs               # System prompt text
    ├── ITool.cs                       # Tool interface
    ├── ICompactionStrategy.cs         # Compaction strategy interface
    ├── SummarizeCompactionStrategy.cs # Summarize compaction implementation
    ├── NoneCompactionStrategy.cs      # No-op compaction
    ├── LoggingConfig.cs               # Serilog logging setup
    ├── Mcp/
    │   ├── McpManager.cs              # MCP server lifecycle management
    │   └── McpToolProxy.cs            # ITool adapter for MCP tools
    └── Tools/
        ├── ToolRegistry.cs            # Tool assembly and registration
        ├── BashTool.cs                # Shell command execution
        ├── ReadFileTool.cs            # File reading (.txt, .docx)
        ├── WriteFileTool.cs           # File writing
        ├── AppendFileTool.cs          # File appending
        ├── HtmlUtilities.cs           # Shared HTML-to-text
        ├── Web/
        │   ├── WebFetchTool.cs        # URL content fetching
        │   ├── WebSearchTool.cs       # Web search (Brave)
        │   ├── ISearchProvider.cs     # Search provider interface
        │   └── BraveSearchProvider.cs # Brave Search API client
        ├── Anthropic/
        │   └── AnthropicUsageTool.cs  # Anthropic Admin API usage reports
        ├── LinkedIn/
        │   ├── LinkedInJobsTool.cs    # Job search
        │   └── LinkedInJobDetailTool.cs # Job detail fetch
        ├── Gmail/
        │   ├── GmailAuth.cs           # OAuth2 flow
        │   ├── GmailParser.cs         # MIME parsing
        │   ├── GmailSearchTool.cs     # Email search
        │   ├── GmailReadTool.cs       # Email reading
        │   └── GmailSendTool.cs       # Email sending
        └── Calendar/
            ├── CalendarAuth.cs            # OAuth2 flow (shared with Gmail)
            ├── CalendarListEventsTool.cs   # List calendar events
            ├── CalendarCreateEventTool.cs  # Create calendar events
            └── CalendarGetEventTool.cs     # Get event details
```
