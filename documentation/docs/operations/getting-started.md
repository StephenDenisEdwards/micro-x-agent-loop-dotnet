# Getting Started

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- An [Anthropic API key](https://console.anthropic.com/)
- (Optional) Google OAuth credentials for Gmail tools

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
```

The Google credentials are optional — if omitted, the Gmail tools will not be registered and all other tools work normally.

### 3. Configure app settings

Edit `src/MicroXAgentLoop/appsettings.json` to set your preferences:

```json
{
  "Model": "claude-sonnet-4-5-20250929",
  "MaxTokens": 8192,
  "DocumentsDirectory": "C:\\path\\to\\your\\documents"
}
```

See [Configuration Reference](appsettings.md) for all available settings.

### 4. Build and run

```bash
cd src/MicroXAgentLoop
dotnet run
```

You should see:

```
micro-x-agent-loop (type 'exit' to quit)
Tools: bash, read_file, write_file, linkedin_jobs, linkedin_job_detail, gmail_search, gmail_read, gmail_send

you>
```

If Google credentials are not configured, the Gmail tools will not appear in the tool list.

## First Use

Try a simple prompt to verify everything works:

```
you> What files are in the current directory?
```

The agent will use the `bash` tool to run `dir` or `ls` and report the results.

### Gmail First Use

The first time you use a Gmail tool, a browser window will open for Google OAuth sign-in. After authorizing, tokens are cached in `.gmail-tokens/` for future sessions.

```
you> Search my Gmail for unread emails from the last 3 days
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
    └── Tools/
        ├── ToolRegistry.cs            # Tool assembly and registration
        ├── BashTool.cs                # Shell command execution
        ├── ReadFileTool.cs            # File reading (.txt, .docx)
        ├── WriteFileTool.cs           # File writing
        ├── HtmlUtilities.cs           # Shared HTML-to-text
        ├── LinkedIn/
        │   ├── LinkedInJobsTool.cs    # Job search
        │   └── LinkedInJobDetailTool.cs # Job detail fetch
        └── Gmail/
            ├── GmailAuth.cs           # OAuth2 flow
            ├── GmailParser.cs         # MIME parsing
            ├── GmailSearchTool.cs     # Email search
            ├── GmailReadTool.cs       # Email reading
            └── GmailSendTool.cs       # Email sending
```
