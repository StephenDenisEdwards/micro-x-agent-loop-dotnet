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
Tools: bash, read_file, write_file, linkedin_jobs, linkedin_job_detail
```

Gmail tools are only registered when both `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `.env`.

**Fix:** Add your Google OAuth credentials to `.env`:
```
GOOGLE_CLIENT_ID=your-id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-your-secret
```

### Gmail OAuth browser window doesn't open

The OAuth flow needs to open a browser for Google sign-in. If you're running in a headless environment or the browser can't launch:

**Fix:** Run the application locally (not via SSH or in a container) for the first OAuth flow. After tokens are cached in `.gmail-tokens/`, the browser is no longer needed.

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
- Start a new session for unrelated tasks
- Set `MaxConversationMessages` to `0` to disable trimming (risk of rate limits)

## Diagnostic Tips

### Check your configuration

Run the app and check the tool list printed at startup. Missing tools indicate missing configuration.

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

Test your API key independently:

```bash
curl https://api.anthropic.com/v1/messages \
  -H "x-api-key: YOUR_KEY" \
  -H "anthropic-version: 2023-06-01" \
  -H "content-type: application/json" \
  -d '{"model":"claude-sonnet-4-5-20250929","max_tokens":10,"messages":[{"role":"user","content":"Hi"}]}'
```
