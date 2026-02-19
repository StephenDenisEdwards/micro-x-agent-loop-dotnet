using ModelContextProtocol.Client;
using Serilog;

namespace MicroXAgentLoop.Mcp;

/// <summary>
/// Manages connections to all configured MCP servers.
/// </summary>
public class McpManager : IAsyncDisposable
{
    private readonly Dictionary<string, McpServerConfig> _serverConfigs;
    private readonly List<McpClient> _clients = [];

    public McpManager(Dictionary<string, McpServerConfig> serverConfigs)
    {
        _serverConfigs = serverConfigs;
    }

    /// <summary>
    /// Connect to all configured MCP servers and return discovered tools.
    /// </summary>
    public async Task<List<ITool>> ConnectAllAsync()
    {
        var allTools = new List<ITool>();

        foreach (var (serverName, config) in _serverConfigs)
        {
            try
            {
                var tools = await ConnectServerAsync(serverName, config);
                allTools.AddRange(tools);
                Log.Information("MCP server '{ServerName}': {Count} tool(s) discovered", serverName, tools.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to connect to MCP server '{ServerName}'", serverName);
            }
        }

        return allTools;
    }

    private async Task<List<ITool>> ConnectServerAsync(string serverName, McpServerConfig config)
    {
        var transport = config.Transport?.ToLowerInvariant() ?? "stdio";

        IClientTransport clientTransport = transport switch
        {
            "stdio" => new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = serverName,
                Command = config.Command!,
                Arguments = config.Args ?? [],
            }),
            "http" => new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(config.Url!),
                Name = serverName,
            }),
            _ => throw new InvalidOperationException(
                $"Unknown transport '{transport}' for MCP server '{serverName}'"),
        };

        var client = await McpClient.CreateAsync(clientTransport);
        _clients.Add(client);

        var mcpTools = await client.ListToolsAsync();

        return mcpTools.Select(tool => (ITool)new McpToolProxy(
            serverName: serverName,
            mcpTool: tool,
            client: client
        )).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing MCP client");
            }
        }
        _clients.Clear();
    }
}

public class McpServerConfig
{
    public string? Transport { get; set; }
    public string? Command { get; set; }
    public string[]? Args { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    public string? Url { get; set; }
}
