# ADR-004: Model Context Protocol (MCP) Integration

## Status

Accepted

## Context

The agent's built-in tool set is fixed at compile time — adding new tools requires code changes, a rebuild, and redeployment. The Python sibling project already supports dynamic tool discovery via the Model Context Protocol (MCP), allowing external servers to expose tools that the agent can call without any code changes.

MCP is an open standard for connecting AI agents to external tool providers. It supports multiple transports (stdio for local processes, HTTP for remote servers) and provides a standard protocol for listing tools, calling them, and receiving results.

We need the .NET agent to support MCP so that:
1. Both agents have feature parity
2. Users can extend the tool set without modifying the agent code
3. External MCP servers (system-info, WhatsApp, etc.) work with both agents

## Decision

Integrate MCP client support using the official C# SDK (`ModelContextProtocol` NuGet package, v0.8.0-preview.1). The implementation consists of two classes:

- **`McpManager`** — manages the lifecycle of MCP server connections. Reads server configurations from `appsettings.json` under the `McpServers` key. Supports `stdio` and `http` transports. Connects to all configured servers at startup, discovers their tools, and provides clean shutdown via `IAsyncDisposable`.

- **`McpToolProxy`** — adapts each MCP tool into the existing `ITool` interface. Tool names are prefixed as `{server}__{tool}` to avoid collisions between servers. Delegates execution to the MCP client's `CallToolAsync` method.

Configuration example:
```json
{
  "McpServers": {
    "system-info": {
      "transport": "stdio",
      "command": "dotnet",
      "args": ["run", "--no-build", "--project", "mcp-servers/system-info"]
    }
  }
}
```

Individual server connection failures are logged but do not block agent startup — this ensures the agent remains usable even if an MCP server is unavailable.

## Consequences

**Easier:**
- Adding new tools without code changes — just configure an MCP server
- Feature parity with the Python agent
- Third-party MCP servers (WhatsApp, databases, etc.) work out of the box
- Tools can be developed and tested independently

**More difficult:**
- The `ModelContextProtocol` package is in preview (0.8.0-preview.1) — API may change before 1.0
- MCP servers must be running before the agent starts (for stdio) or accessible (for HTTP)
- Debugging tool calls involves an additional indirection layer
- Tool names become longer (`server__tool` format)
