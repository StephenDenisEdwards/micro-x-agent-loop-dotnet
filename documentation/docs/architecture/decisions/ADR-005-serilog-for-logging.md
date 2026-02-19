# ADR-005: Serilog for Structured Logging

## Status

Accepted

## Context

The agent previously used `Console.Error.WriteLine` for all diagnostic output — rate limit retries, compaction summaries, token usage, and truncation warnings. This approach has several limitations:

1. No log levels — all output has equal priority; no way to filter noise
2. No file logging — diagnostic history is lost when the terminal closes
3. No structured data — messages are plain strings, not queryable
4. No rotation — if file logging were added manually, there's no built-in rotation/retention

The Python sibling project uses `loguru` with configurable console + file sinks, log levels, and rotation. We need equivalent capability in .NET.

## Decision

Use **Serilog** for structured logging with configurable sinks. Serilog is the most widely-used structured logging library for .NET and is the closest analog to Python's loguru.

Configuration is read from `appsettings.json`:

```json
{
  "LogLevel": "Debug",
  "LogConsumers": [
    { "type": "console" },
    { "type": "file", "path": "agent.log" }
  ]
}
```

The `LoggingConfig.SetupLogging()` method:
- Parses the log level (supports Verbose/Debug/Information/Warning/Error/Fatal with aliases like "info", "warn")
- Configures each consumer as a Serilog sink
- Console sink writes to stderr (matching the existing pattern)
- File sink uses 10 MB rotation and retains 3 files
- Returns human-readable descriptions for the startup display
- Falls back to console + file if no consumers are configured

NuGet packages added: `Serilog`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`.

## Consequences

**Easier:**
- Filter diagnostic output by level (e.g., only show warnings in production)
- Persistent log files with automatic rotation
- MCP tool calls and compaction events are logged with structured context
- Feature parity with Python agent's loguru configuration
- Standard .NET logging patterns that other developers expect

**More difficult:**
- Three new NuGet dependencies
- Existing `Console.Error.WriteLine` calls in LlmClient and SummarizeCompactionStrategy should be migrated to Serilog over time
- Log level naming differs slightly from Python (Information vs INFO, Verbose vs TRACE)
