# ADR-008: Architecture Refactoring — Base Classes, Shared Infrastructure, and Program.cs Decomposition

## Status

Accepted

## Context

After adding Google Contacts tools (ADR-007), the codebase had grown to 20+ tools across three Google API services (Gmail, Calendar, Contacts) plus web, LinkedIn, and MCP tools. Several patterns of duplication and inconsistency had accumulated:

1. **Google auth duplication.** Three nearly identical auth classes (`GmailAuth`, `CalendarAuth`, `ContactsAuth`) each implemented the same OAuth2 browser flow, token caching, and service construction — differing only in scopes, token directory, and service type. Each used `lock` or manual null-checking that was not thread-safe under concurrent first-use.

2. **Google tool constructor duplication.** All 12 Google tools repeated the same `(string googleClientId, string googleClientSecret)` constructor signature, stored credentials in fields, and implemented `ITool` directly. Error handling varied — some used `Console.Error.WriteLine`, some returned error strings silently.

3. **Retry pipeline duplication.** The Polly retry pipeline for Anthropic API calls (rate limits, connection errors, timeouts with exponential backoff) was copy-pasted verbatim between `LlmClient.cs` and `SummarizeCompactionStrategy.cs` — 28 identical lines.

4. **HttpClient sprawl.** Five separate `static HttpClient` instances were scattered across tools (`WebFetchTool`, `BraveSearchProvider`, `LinkedInJobsTool`, `LinkedInJobDetailTool`, `AnthropicUsageTool`), some with no timeout configured, some with duplicated browser headers.

5. **Program.cs as god class.** The entry point handled configuration loading, tool assembly, MCP connection, compaction setup, startup display, and the REPL loop — 170 lines of mixed concerns.

6. **Inconsistent logging.** Some code used `Console.Error.WriteLine`, some used `Serilog.Log`, making it hard to filter or redirect operational messages.

7. **Magic numbers.** `SummarizeCompactionStrategy` used hard-coded values (200, 500, 700, 4096, 100_000, 50_000) without names, making the code harder to understand and tune.

8. **No MCP retry.** `McpToolProxy` had no retry logic, meaning any transient failure in an MCP tool call was a hard failure.

Four approaches were considered for addressing this:

1. **Do nothing** — accept the duplication as manageable.
2. **Targeted fixes only** — fix the thread-safety bug and add MCP retry, leave the rest.
3. **Full DI container** — add `Microsoft.Extensions.DependencyInjection`, make everything injectable.
4. **Extract base classes and shared infrastructure** — create base classes and shared utilities to eliminate duplication, without adding a DI framework.

## Decision

Apply option 4: extract base classes and shared infrastructure. This eliminates the duplication and fixes the bugs without adding a DI framework, which would be over-engineering for a console application of this size.

The refactoring was implemented in four phases:

### Phase 1: Google base classes

- **`GoogleAuthBase<TService>`** (`Tools/GoogleAuthBase.cs`) — Generic abstract base class for all Google API authentication. Uses `SemaphoreSlim` with double-checked locking for thread-safe lazy initialization. Subclasses only need to specify `Scopes`, `TokenDirectory`, and a `CreateService()` factory method. The three auth classes (`GmailAuth`, `CalendarAuth`, `ContactsAuth`) now extend this base and expose a singleton via `public static readonly Instance`.

- **`GoogleToolBase`** (`Tools/GoogleToolBase.cs`) — Abstract base class for all Google OAuth tools. Holds `GoogleClientId` and `GoogleClientSecret` fields, implements `ITool`, and provides a `HandleError()` method that logs via Serilog and returns a formatted error string. All 12 Google tools now extend this instead of implementing `ITool` directly.

### Phase 2: Shared retry pipeline and HTTP clients

- **`RetryPipelineFactory`** (`RetryPipelineFactory.cs`) — Static factory that creates the shared Polly `ResiliencePipeline` for Anthropic API calls. Handles HTTP 429 (rate limit), connection errors, and timeouts with exponential backoff. Logging uses Serilog instead of `Console.Error.WriteLine`. Both `LlmClient` and `SummarizeCompactionStrategy` now call `RetryPipelineFactory.Create()`.

- **`HttpClientFactory`** (`Tools/HttpClientFactory.cs`) — Two pre-configured `HttpClient` instances: `Browser` (with browser-like User-Agent, Accept, and Accept-Language headers, redirect following, and 30-second timeout) and `Api` (with 30-second timeout only). This replaces five separate `static HttpClient` instances across tools, ensures all have proper timeouts, and eliminates duplicated browser header setup.

### Phase 3: Program.cs decomposition

- **`ConfigLoader`** (`ConfigLoader.cs`) — Loads and validates all configuration from `IConfiguration` and environment variables into a typed `AppConfig` record. Includes MCP server config parsing.

- **`StartupDisplay`** (`StartupDisplay.cs`) — Renders the startup banner showing tool list, MCP servers, working directory, compaction config, and logging sinks.

- **`Program.cs`** — Reduced to a thin composition root that calls `ConfigLoader.Load()`, assembles tools, connects MCP, and runs the REPL loop.

### Phase 4: MCP retry, logging, and constant extraction

- **MCP retry** — `McpToolProxy` now wraps `CallToolAsync` in a Polly retry pipeline (2 retries, exponential backoff from 2 seconds) that handles `HttpRequestException`, `TaskCanceledException`, and `TimeoutException`.

- **Logging standardization** — All `Console.Error.WriteLine` calls in `Agent.cs`, `LlmClient.cs`, and `SummarizeCompactionStrategy.cs` were replaced with `Serilog.Log` calls using structured log templates.

- **Named constants** — Magic numbers in `SummarizeCompactionStrategy` were replaced with named constants: `CharsPerToken`, `ToolInputPreviewChars`, `ToolResultPreviewChars`, `ToolResultHeadChars`, `ToolResultTailChars`, `MaxSummarizationInputChars`, `SummarizationMaxTokens`.

## Consequences

**Easier:**

- Adding a new Google API service (e.g., Google Drive) now requires only a 10-line auth subclass and tool subclasses extending `GoogleToolBase` — no boilerplate to copy
- Thread-safe Google auth initialization — the `SemaphoreSlim` + double-checked locking pattern in `GoogleAuthBase` prevents the race condition that existed in the original auth classes
- Consistent error handling — all Google tools use `HandleError()` which logs via Serilog and returns a formatted error string
- Changing retry behavior is a single edit in `RetryPipelineFactory` instead of updating two files
- MCP tool calls now survive transient network failures
- All operational logging goes through Serilog, so it can be filtered, formatted, and routed consistently
- `Program.cs` is easier to read — configuration loading and startup display are each in focused files
- Compaction tuning — all thresholds are named constants with doc comments

**Harder:**

- Developers must understand the `GoogleAuthBase<T>` → auth subclass → `GoogleToolBase` → tool subclass inheritance hierarchy when working on Google tools
- The `HttpClientFactory` pattern means changing HTTP configuration (e.g., adding a specific header for one tool) requires either using the shared client as-is or creating a one-off `HttpRequestMessage` with custom headers
- `RetryPipelineFactory.Create()` returns a new pipeline instance each time — callers should cache it in a `static readonly` field (both current callers already do this)
