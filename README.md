# micro-x-agent-loop-dotnet

A minimal AI agent loop built with .NET 8 and the Anthropic Claude API. The agent runs in a REPL, takes natural-language prompts, and autonomously calls tools to get things done. Responses stream in real time as Claude generates them.

## Features

- **Streaming responses** — text appears word-by-word as Claude generates it
- **Parallel tool execution** — multiple tool calls in a single turn run concurrently
- **Automatic retry** — Polly-based exponential backoff on API rate limits
- **Configurable limits** — max tool result size and conversation history length with clear warnings
- **Token compaction** — LLM-based conversation summarization to preserve context intelligently
- **Conditional tools** — Gmail, Calendar, web search, and usage tools only load when credentials are present
- **MCP integration** — dynamic tool discovery from external servers via Model Context Protocol
- **Structured logging** — Serilog-based configurable console + file sinks
- **Cross-platform** — works on Windows, macOS, and Linux

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An [Anthropic API key](https://console.anthropic.com/)
- (Optional) Google OAuth credentials for Gmail, Calendar, and Contacts tools
- (Optional) Brave Search API key for web search
- (Optional) Anthropic Admin API key for usage reports

### 1. Clone and configure

```bash
git clone https://github.com/StephenDenisEdwards/micro-x-agent-loop-dotnet.git
cd micro-x-agent-loop-dotnet
```

Create a `.env` file in `src/MicroXAgentLoop/`:

```
ANTHROPIC_API_KEY=sk-ant-...
GOOGLE_CLIENT_ID=your-client-id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=your-client-secret
BRAVE_API_KEY=BSA...
ANTHROPIC_ADMIN_API_KEY=sk-ant-admin-...
```

All credentials except `ANTHROPIC_API_KEY` are optional — if omitted, their associated tools are simply not registered.

### 2. Run

```bash
cd src/MicroXAgentLoop
dotnet run
```

You'll see:

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
  - contacts_search
  - contacts_list
  - contacts_get
  - contacts_create
  - contacts_update
  - contacts_delete
  - anthropic_usage
  - web_search
MCP servers:
  - system-info: system_info, disk_info, network_info
Working directory: C:\Users\steph\source\repos\resources\documents
Compaction: summarize (threshold: 80,000 tokens, tail: 6 messages)
Logging: console (stderr, Debug), file (agent.log, Debug)

you>
```

### Configuration

App settings live in `src/MicroXAgentLoop/appsettings.json`:

```json
{
  "Model": "claude-sonnet-4-5-20250929",
  "MaxTokens": 8192,
  "Temperature": 1.0,
  "MaxToolResultChars": 40000,
  "MaxConversationMessages": 50,
  "WorkingDirectory": "C:\\path\\to\\your\\documents",
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
      "args": ["run", "--no-build", "--project", "mcp-servers/system-info"]
    }
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `Model` | Claude model ID | `claude-sonnet-4-5-20250929` |
| `MaxTokens` | Max tokens per response | `8192` |
| `Temperature` | Sampling temperature (0.0–1.0) | `1.0` |
| `MaxToolResultChars` | Max characters per tool result before truncation | `40000` |
| `MaxConversationMessages` | Max messages in history before trimming oldest | `50` |
| `WorkingDirectory` | Working directory for tools (bash cwd, file path resolution) | _(none)_ |
| `CompactionStrategy` | Context compaction: `"none"` or `"summarize"` | `"none"` |
| `CompactionThresholdTokens` | Estimated token count that triggers compaction | `80000` |
| `ProtectedTailMessages` | Recent messages to preserve during compaction | `6` |
| `LogLevel` | Minimum log level | `"Information"` |
| `LogConsumers` | Logging sinks (console, file) | console + file |
| `McpServers` | MCP server configurations | _(none)_ |

All settings are optional — sensible defaults are used when missing. See [Configuration Reference](documentation/docs/operations/appsettings.md) for full details.

Secrets (API keys) stay in `.env` and are loaded by DotNetEnv.

## Tools

### File System

| Tool | Description |
|------|-------------|
| `bash` | Execute shell commands (cmd.exe on Windows, bash on Unix). 30-second timeout. |
| `read_file` | Read text files and `.docx` documents. Resolves relative paths via WorkingDirectory. |
| `write_file` | Write content to a file, creating parent directories as needed. |
| `append_file` | Append content to existing files. |

### Web

| Tool | Description |
|------|-------------|
| `web_fetch` | Fetch content from a URL and return as readable text. Supports HTML (converted to plain text), JSON (pretty-printed), and plain text. |
| `web_search` | Search the web via Brave Search API. Returns titles, URLs, and descriptions. *(Requires `BRAVE_API_KEY`)* |

### LinkedIn

| Tool | Description |
|------|-------------|
| `linkedin_jobs` | Search LinkedIn job postings by keyword, location, date, job type, remote filter, experience level. |
| `linkedin_job_detail` | Fetch the full job description from a LinkedIn job URL. |

### Gmail (conditional)

| Tool | Description |
|------|-------------|
| `gmail_search` | Search Gmail using Gmail search syntax. *(Requires Google credentials)* |
| `gmail_read` | Read the full content of a Gmail message by ID. |
| `gmail_send` | Send a plain-text email from your Gmail account. |

### Google Calendar (conditional)

| Tool | Description |
|------|-------------|
| `calendar_list_events` | List events by date range or search query. *(Requires Google credentials)* |
| `calendar_create_event` | Create events with title, time, attendees, description. |
| `calendar_get_event` | Get full event details by ID. |

### Google Contacts (conditional)

| Tool | Description |
|------|-------------|
| `contacts_search` | Search contacts by name, email, or phone number. *(Requires Google credentials)* |
| `contacts_list` | List contacts with pagination and sort options. |
| `contacts_get` | Get full contact details by resource name. |
| `contacts_create` | Create a new contact with name, email, phone, organization. |
| `contacts_update` | Update an existing contact (requires etag for concurrency). |
| `contacts_delete` | Delete a contact by resource name. |

### Anthropic Admin (conditional)

| Tool | Description |
|------|-------------|
| `anthropic_usage` | Query usage, cost, and Claude Code productivity reports via Anthropic Admin API. *(Requires `ANTHROPIC_ADMIN_API_KEY`)* |

### MCP Tools (dynamic)

Tools discovered from configured MCP servers. Each tool is namespaced as `{server}__{tool}` to avoid name collisions.

## MCP (Model Context Protocol) Support

The agent can connect to MCP servers to discover and use external tools dynamically. Supported transports:

- **stdio** — spawns a local process (command + args)
- **http** — connects to a Streamable HTTP endpoint

Configure MCP servers in `appsettings.json`:

```json
{
  "McpServers": {
    "system-info": {
      "transport": "stdio",
      "command": "dotnet",
      "args": ["run", "--no-build", "--project", "mcp-servers/system-info"]
    },
    "remote-server": {
      "transport": "http",
      "url": "http://localhost:3000/mcp"
    }
  }
}
```

Individual server failures are logged but don't block agent startup.

## Example Prompts

### File operations

```
Read the file documents/Stephen Edwards CV December 2025.docx and summarise it
```

### Web

```
Fetch the content of https://example.com and summarise it
Search the web for "latest .NET 9 features" and give me the top 5 results
```

### LinkedIn job search

```
Search LinkedIn for remote senior .NET developer jobs posted in the last week
Get the full job description for the first result
```

### Gmail

```
Search my Gmail for unread emails from the last 3 days
Read the email with subject "Interview Invitation" and summarise it
```

### Calendar

```
List my calendar events for next week
Create a meeting for tomorrow at 2pm with alice@example.com
```

### Contacts

```
Search my contacts for "John Smith"
Create a new contact for Jane Doe with email jane@example.com and phone 07700900123
```

### Usage reports

```
Show my Anthropic API usage for the last 7 days grouped by model
```

### Multi-step tasks

```
Read my CV, search LinkedIn for .NET jobs in London posted this week, and write a cover letter for the best match
```

## Architecture

```
Program.cs              -- Entry point: loads config, builds tools, connects MCP, starts REPL
Agent.cs                -- Agent loop: streaming, parallel tool dispatch, history management
AgentConfig.cs          -- Immutable configuration record
LlmClient.cs            -- Anthropic API streaming + Polly retry pipeline
SystemPrompt.cs         -- Dynamic system prompt
LoggingConfig.cs        -- Serilog logging setup
ITool.cs                -- Tool interface
ICompactionStrategy.cs  -- Compaction strategy interface
SummarizeCompactionStrategy.cs -- LLM-based conversation summarization
NoneCompactionStrategy.cs -- No-op compaction
Mcp/
  McpManager.cs         -- MCP server connection lifecycle
  McpToolProxy.cs       -- MCP-to-ITool adapter
Tools/
  ToolRegistry.cs       -- Tool assembly with conditional registration
  BashTool.cs
  ReadFileTool.cs
  WriteFileTool.cs
  AppendFileTool.cs
  HtmlUtilities.cs
  Web/
    WebFetchTool.cs     -- URL content fetching
    WebSearchTool.cs    -- Web search via providers
    ISearchProvider.cs  -- Search provider abstraction
    BraveSearchProvider.cs -- Brave Search API
  LinkedIn/
    LinkedInJobsTool.cs
    LinkedInJobDetailTool.cs
  Gmail/
    GmailAuth.cs
    GmailParser.cs
    GmailSearchTool.cs
    GmailReadTool.cs
    GmailSendTool.cs
  Calendar/
    CalendarAuth.cs
    CalendarListEventsTool.cs
    CalendarCreateEventTool.cs
    CalendarGetEventTool.cs
  Contacts/
    ContactsAuth.cs
    ContactsFormatter.cs
    ContactsSearchTool.cs
    ContactsListTool.cs
    ContactsGetTool.cs
    ContactsCreateTool.cs
    ContactsUpdateTool.cs
    ContactsDeleteTool.cs
  Anthropic/
    AnthropicUsageTool.cs -- Anthropic Admin API reports
```

## Documentation

Full documentation lives in [`documentation/docs/`](documentation/docs/index.md):

- [**Software Architecture Document**](documentation/docs/architecture/SAD.md) — system overview, components, data flow (arc42 lite)
- [**Architecture Decision Records**](documentation/docs/architecture/decisions/README.md) — ADRs for secrets, retry, streaming, MCP, contacts
- [**Agent Loop Design**](documentation/docs/design/DESIGN-agent-loop.md) — core loop, parallel execution, streaming, compaction
- [**Tool System Design**](documentation/docs/design/DESIGN-tool-system.md) — ITool interface, registry, built-in tools, MCP integration
- [**Compaction Design**](documentation/docs/design/DESIGN-compaction.md) — LLM-based conversation summarization strategy
- [**Map-Evaluate Pattern**](documentation/docs/design/DESIGN-map-evaluate-pattern.md) — isolated scoring for criteria matching workflows
- [**Account Management APIs**](documentation/docs/design/DESIGN-account-management-apis.md) — Anthropic/OpenAI admin API catalog
- [**Planning Documents**](documentation/docs/planning/) — web fetch, web search, browser automation, memory, WhatsApp
- [**Tool READMEs**](documentation/docs/design/tools/) — individual tool documentation
- [**Example Prompts**](documentation/docs/examples/README.md) — working prompt templates for common tasks
- [**Getting Started**](documentation/docs/operations/getting-started.md) — setup, prerequisites, first run
- [**Configuration Reference**](documentation/docs/operations/appsettings.md) — all settings with types and defaults
- [**Troubleshooting**](documentation/docs/operations/troubleshooting.md) — common issues and solutions

## License

MIT
