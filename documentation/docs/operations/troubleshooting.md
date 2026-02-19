# Troubleshooting

## Common Issues

### "ANTHROPIC_API_KEY environment variable is required"

The `.env` file is missing or doesn't contain the key.

**Fix:** Create `src/MicroXAgentLoop/.env` with:
```
ANTHROPIC_API_KEY=sk-ant-your-key-here
```

Make sure the `.env` file is in the same directory as the `.csproj` file, or in the working directory when running.

### Rate limit error (HTTP 429)

```
Rate limited. Retrying in 10s (attempt 1/5)...
```

The Anthropic API has per-minute token limits. The agent retries automatically with exponential backoff.

**If it keeps failing:**
- Wait a minute and try again with a shorter prompt
- Lower `MaxTokens` in `appsettings.json` to reduce response size
- Check your API tier at https://console.anthropic.com/
- Consider upgrading your API plan for higher rate limits

### Gmail tools not appearing

```
Tools:
  - bash
  - read_file
  - write_file
  - append_file
  - linkedin_jobs
  - linkedin_job_detail
  - web_fetch
```

Gmail and Calendar tools are only registered when both `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `.env`.

**Fix:** Add your Google OAuth credentials to `.env`:
```
GOOGLE_CLIENT_ID=your-id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-your-secret
```

### Gmail OAuth browser window doesn't open

The OAuth flow needs to open a browser for Google sign-in. If you're running in a headless environment or the browser can't launch:

**Fix:** Run the application locally (not via SSH or in a container) for the first OAuth flow. After tokens are cached in `.gmail-tokens/`, the browser is no longer needed.

### Contacts tools returning "People API has not been used" error

The Google People API must be enabled in the same Google Cloud project used for Gmail and Calendar.

**Fix:**
1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Select the project used for your OAuth credentials
3. Navigate to **APIs & Services** → **Library**
4. Search for "People API" and click **Enable**
5. Restart the agent

The first time you use a Contacts tool, a separate OAuth consent flow will open in your browser (Contacts uses different scopes than Gmail/Calendar). Tokens are cached in `.contacts-tokens/`.

### Web search not working

The `web_search` tool does not appear in the startup tool list.

**Cause:** The `BRAVE_API_KEY` environment variable is not set in `.env`. The `web_search` tool requires a Brave Search API key and is only registered when the key is present.

**Fix:** Add your Brave API key to `.env`:
```
BRAVE_API_KEY=your-brave-api-key-here
```

You can get a free API key at https://brave.com/search/api/. After adding the key, restart the agent and `web_search` should appear in the tool list.

If the key is set but searches return errors, check:
- The key is valid and has not expired
- Your Brave Search API plan has remaining quota
- Network connectivity to `api.search.brave.com`

### Anthropic usage tool not appearing

The `anthropic_usage` tool does not appear in the startup tool list.

**Cause:** The `ANTHROPIC_ADMIN_API_KEY` environment variable is not set in `.env`. This tool requires an Anthropic Admin API key (separate from the regular API key).

**Fix:** Add your Admin API key to `.env`:
```
ANTHROPIC_ADMIN_API_KEY=your-admin-key-here
```

The Admin API key can be generated in the Anthropic Console under your organization settings. It is different from `ANTHROPIC_API_KEY` — the regular key is for model inference, while the admin key is for usage and cost reports.

If the tool is registered but returns errors:
- Verify the key has admin permissions (not a regular API key)
- Check that the `starting_at` parameter uses the correct format (RFC 3339 for usage/cost, YYYY-MM-DD for claude_code)

### MCP server connection failures

At startup, you see an error like:

```
[ERR] Failed to connect to MCP server 'my-server'
```

**Common causes and fixes:**

1. **Command not found** — The `command` in your MCP server config is not on the system PATH.
   - Verify the command exists: run it manually in a terminal
   - Use an absolute path to the executable if needed

2. **Project not built** — For stdio servers using `dotnet run --no-build`, the project must be pre-built.
   - Build the MCP server project first: `dotnet build path/to/server`

3. **Wrong arguments** — The `args` array in your config is incorrect.
   - Check the MCP server's documentation for correct command-line arguments

4. **HTTP server not running** — For HTTP transport, the remote server must be running before the agent starts.
   - Start the MCP server first, then start the agent
   - Verify the URL is correct and the port is not blocked

5. **Environment variables missing** — The MCP server requires environment variables not provided in the `env` config.
   - Add required env vars to the `env` object in your MCP server config

The agent continues to start even when an MCP server fails to connect. Other tools and MCP servers are unaffected.

### Logging not appearing / log file not created

**Console logs not visible:**

Serilog writes console logs to stderr, not stdout. If you are redirecting only stdout, logs will not appear in the redirected output.

**Fix:** To capture logs when redirecting:
```bash
dotnet run 2>errors.log        # redirect stderr separately
dotnet run 2>&1 | tee all.log  # capture both stdout and stderr
```

**Log file not created:**

The file sink creates the log file on the first log write, not at startup. If no messages meet the minimum log level, no file is created.

**Fix:**
- Lower `LogLevel` in `appsettings.json` (e.g., to `"Debug"`) to capture more messages
- Verify the file path in `LogConsumers` is writable
- If using a subdirectory in the path (e.g., `"logs/agent.log"`), the directory is created automatically, but the parent must exist

**No logging configured:**

If `LogConsumers` is omitted entirely from `appsettings.json`, the default is both console (stderr) and file (`agent.log`) sinks. If you set `LogConsumers` to an empty array, no logging occurs.

**Fix:** Either omit `LogConsumers` to use defaults, or explicitly configure at least one sink:
```json
"LogConsumers": [
  { "type": "console" },
  { "type": "file", "path": "agent.log" }
]
```

### Compaction messages in stderr

```
  Compaction: estimated ~95,000 tokens, threshold 80,000 — compacting 12 messages
  Compaction: summarized 12 messages into ~1,200 tokens, freed ~45,000 estimated tokens
```

**This is expected behavior.** When `CompactionStrategy` is set to `"summarize"`, the agent periodically summarizes older conversation history to stay within token limits. These progress messages are written to stderr so they don't interfere with the conversation output on stdout.

**If compaction triggers too frequently:**
- Increase `CompactionThresholdTokens` in `appsettings.json` (default: 80000)
- The higher the threshold, the more context is kept before compaction triggers

**If compaction fails:**

```
  Warning: Compaction failed: <error>. Falling back to history trimming.
```

This means the summarization API call failed (typically due to rate limiting). The agent falls back to keeping the conversation as-is. If this happens repeatedly, the conversation will eventually hit `MaxConversationMessages` and be trimmed from the oldest messages.

**To disable compaction entirely:**
```json
"CompactionStrategy": "none"
```

### "Could not extract job description from the page"

LinkedIn may have blocked the scraping request or changed their HTML structure.

**Possible causes:**
- LinkedIn rate limiting (too many requests in a short time)
- LinkedIn A/B testing different page layouts
- IP-based blocking

**Fix:** Wait a few minutes and try again. If persistent, the CSS selectors in `LinkedInJobDetailTool.cs` may need updating.

### Build fails with file lock error

```
error MSB3027: Could not copy ... The file is locked by: "MicroXAgentLoop (PID)"
```

A running instance of the application is locking the output files.

**Fix:** Close the running instance of MicroXAgentLoop, then rebuild.

### "OUTPUT TRUNCATED" message in tool results

```
Warning: read_file output truncated from 85,000 to 40,000 chars
```

A tool returned more text than the configured `MaxToolResultChars` limit.

**This is expected behavior.** The truncation prevents excessive token usage. If you need the full output:
- Increase `MaxToolResultChars` in `appsettings.json`
- Ask the agent to read a specific section of the file
- Set `MaxToolResultChars` to `0` to disable truncation (use with caution)

### "Conversation history trimmed" message

```
Note: Conversation history trimmed — removed 2 oldest message(s) to stay within the 50 message limit
```

The conversation has exceeded `MaxConversationMessages`. Oldest messages were removed to stay within the limit.

**This is expected behavior.** It prevents rate limit errors from growing context. If you need longer conversations:
- Increase `MaxConversationMessages` in `appsettings.json`
- Enable `CompactionStrategy: "summarize"` to preserve context through summarization instead of hard trimming
- Start a new session for unrelated tasks
- Set `MaxConversationMessages` to `0` to disable trimming (risk of rate limits)

## Diagnostic Tips

### Check your configuration

Run the app and check the startup output. The tool list, MCP servers, working directory, compaction, and logging status are all printed at startup. Missing tools indicate missing configuration (API keys in `.env`).

### Check `.env` is being loaded

The `.env` file must be in the working directory when `dotnet run` executes. If you run from the repo root:

```bash
dotnet run --project src/MicroXAgentLoop
```

The working directory is the repo root, but `DotNetEnv.Env.Load()` looks in the current directory. Run from the project directory instead:

```bash
cd src/MicroXAgentLoop
dotnet run
```

### Check API key validity

Test your Anthropic API key independently:

```bash
curl https://api.anthropic.com/v1/messages \
  -H "x-api-key: YOUR_KEY" \
  -H "anthropic-version: 2023-06-01" \
  -H "content-type: application/json" \
  -d '{"model":"claude-sonnet-4-5-20250929","max_tokens":10,"messages":[{"role":"user","content":"Hi"}]}'
```

### Check log output for details

When something goes wrong, check the log output for detailed error information:
- **Console logs** appear on stderr during the session
- **File logs** are written to the path configured in `LogConsumers` (default: `agent.log` in the working directory)

Lower `LogLevel` to `"Debug"` for more detailed diagnostic output, including MCP tool calls, API request details, and compaction decisions.
