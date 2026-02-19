using Microsoft.Extensions.Configuration;
using MicroXAgentLoop.Mcp;
using MicroXAgentLoop.Tools;

namespace MicroXAgentLoop;

/// <summary>
/// Loads and validates application configuration from environment variables and appsettings.json.
/// </summary>
public static class ConfigLoader
{
    private const string DefaultModel = "claude-sonnet-4-5-20250929";

    public record AppConfig(
        string ApiKey,
        string Model,
        int MaxTokens,
        decimal Temperature,
        int MaxToolResultChars,
        int MaxConversationMessages,
        string? WorkingDirectory,
        string CompactionStrategy,
        int CompactionThresholdTokens,
        int ProtectedTailMessages,
        string? GoogleClientId,
        string? GoogleClientSecret,
        string? AnthropicAdminApiKey,
        string? BraveApiKey,
        Dictionary<string, McpServerConfig> McpServers);

    public static AppConfig Load(IConfiguration configuration)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is required.");

        var mcpServers = LoadMcpServers(configuration);

        return new AppConfig(
            ApiKey: apiKey,
            Model: configuration["Model"] ?? DefaultModel,
            MaxTokens: int.TryParse(configuration["MaxTokens"], out var mt) ? mt : 8192,
            Temperature: decimal.TryParse(configuration["Temperature"], out var temp) ? temp : 1.0m,
            MaxToolResultChars: int.TryParse(configuration["MaxToolResultChars"], out var trc) ? trc : 40_000,
            MaxConversationMessages: int.TryParse(configuration["MaxConversationMessages"], out var mcm) ? mcm : 50,
            WorkingDirectory: configuration["WorkingDirectory"],
            CompactionStrategy: configuration["CompactionStrategy"] ?? "none",
            CompactionThresholdTokens: int.TryParse(configuration["CompactionThresholdTokens"], out var ctt) ? ctt : 80_000,
            ProtectedTailMessages: int.TryParse(configuration["ProtectedTailMessages"], out var ptm) ? ptm : 6,
            GoogleClientId: Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"),
            GoogleClientSecret: Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET"),
            AnthropicAdminApiKey: Environment.GetEnvironmentVariable("ANTHROPIC_ADMIN_API_KEY"),
            BraveApiKey: Environment.GetEnvironmentVariable("BRAVE_API_KEY"),
            McpServers: mcpServers);
    }

    private static Dictionary<string, McpServerConfig> LoadMcpServers(IConfiguration configuration)
    {
        var mcpSection = configuration.GetSection("McpServers");
        var configs = new Dictionary<string, McpServerConfig>();

        foreach (var serverSection in mcpSection.GetChildren())
        {
            var serverConfig = new McpServerConfig
            {
                Transport = serverSection["transport"],
                Command = serverSection["command"],
                Args = serverSection.GetSection("args").GetChildren().Select(c => c.Value!).ToArray(),
                Url = serverSection["url"],
            };

            var envSection = serverSection.GetSection("env");
            if (envSection.GetChildren().Any())
            {
                serverConfig.Env = new Dictionary<string, string>();
                foreach (var envItem in envSection.GetChildren())
                    serverConfig.Env[envItem.Key] = envItem.Value!;
            }

            configs[serverSection.Key] = serverConfig;
        }

        return configs;
    }
}
