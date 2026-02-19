using Microsoft.Extensions.Configuration;
using MicroXAgentLoop;
using MicroXAgentLoop.Mcp;
using MicroXAgentLoop.Tools;
using Serilog;

DotNetEnv.Env.Load();

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

// Set up structured logging
var logDescriptions = LoggingConfig.SetupLogging(configuration);

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("Error: ANTHROPIC_API_KEY environment variable is required.");
    Environment.Exit(1);
}

var model = configuration["Model"] ?? "claude-sonnet-4-5-20250929";
var maxTokens = int.TryParse(configuration["MaxTokens"], out var mt) ? mt : 8192;
var temperature = decimal.TryParse(configuration["Temperature"], out var temp) ? temp : 1.0m;
var maxToolResultChars = int.TryParse(configuration["MaxToolResultChars"], out var trc) ? trc : 40_000;
var maxConversationMessages = int.TryParse(configuration["MaxConversationMessages"], out var mcm) ? mcm : 50;
var workingDirectory = configuration["WorkingDirectory"];
var compactionStrategyName = configuration["CompactionStrategy"] ?? "none";
var compactionThresholdTokens = int.TryParse(configuration["CompactionThresholdTokens"], out var ctt) ? ctt : 80_000;
var protectedTailMessages = int.TryParse(configuration["ProtectedTailMessages"], out var ptm) ? ptm : 6;
var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
var googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
var anthropicAdminApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_ADMIN_API_KEY");
var braveApiKey = Environment.GetEnvironmentVariable("BRAVE_API_KEY");

var tools = ToolRegistry.GetAll(workingDirectory, googleClientId, googleClientSecret, anthropicAdminApiKey, braveApiKey);
var allTools = new List<ITool>(tools);

// Connect to MCP servers
McpManager? mcpManager = null;
var mcpTools = new List<ITool>();
var mcpSection = configuration.GetSection("McpServers");
var mcpServerConfigs = new Dictionary<string, McpServerConfig>();

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

    mcpServerConfigs[serverSection.Key] = serverConfig;
}

if (mcpServerConfigs.Count > 0)
{
    mcpManager = new McpManager(mcpServerConfigs);
    mcpTools = await mcpManager.ConnectAllAsync();
    allTools.AddRange(mcpTools);
}

ICompactionStrategy? compactionStrategy = compactionStrategyName.ToLowerInvariant() switch
{
    "summarize" => new SummarizeCompactionStrategy(
        LlmClient.CreateClient(apiKey),
        model,
        compactionThresholdTokens,
        protectedTailMessages),
    _ => null,
};

var agent = new Agent(new AgentConfig(
    Model: model,
    MaxTokens: maxTokens,
    Temperature: temperature,
    ApiKey: apiKey,
    Tools: allTools,
    SystemPrompt: SystemPrompt.GetText(),
    MaxToolResultChars: maxToolResultChars,
    MaxConversationMessages: maxConversationMessages,
    CompactionStrategy: compactionStrategy));

// Enhanced startup display (matching Python agent)
Console.WriteLine("micro-x-agent-loop (type 'exit' to quit)");

var builtinTools = allTools.Where(t => !mcpTools.Contains(t)).ToList();
Console.WriteLine("Tools:");
foreach (var t in builtinTools)
    Console.WriteLine($"  - {t.Name}");

if (mcpTools.Count > 0)
{
    var mcpNames = new Dictionary<string, List<string>>();
    foreach (var t in mcpTools)
    {
        var parts = t.Name.Split("__", 2);
        var server = parts[0];
        var toolName = parts.Length > 1 ? parts[1] : t.Name;
        if (!mcpNames.ContainsKey(server))
            mcpNames[server] = [];
        mcpNames[server].Add(toolName);
    }
    Console.WriteLine("MCP servers:");
    foreach (var (server, toolNames) in mcpNames)
        Console.WriteLine($"  - {server}: {string.Join(", ", toolNames)}");
}

if (!string.IsNullOrEmpty(workingDirectory))
    Console.WriteLine($"Working directory: {workingDirectory}");

if (compactionStrategyName != "none")
    Console.WriteLine($"Compaction: {compactionStrategyName} (threshold: {compactionThresholdTokens:N0} tokens, tail: {protectedTailMessages} messages)");

if (logDescriptions.Count > 0)
    Console.WriteLine($"Logging: {string.Join(", ", logDescriptions)}");

Console.WriteLine();

try
{
    while (true)
    {
        Console.Write("you> ");
        var input = Console.ReadLine();

        if (input is null)
            break;

        var trimmed = input.Trim();

        if (trimmed is "exit" or "quit")
            break;

        if (string.IsNullOrEmpty(trimmed))
            continue;

        try
        {
            Console.WriteLine();
            await agent.RunAsync(trimmed);
            Console.WriteLine("\n");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled error");
            Console.Error.WriteLine($"\nError: {ex.Message}\n");
        }
    }
}
finally
{
    if (mcpManager is not null)
        await mcpManager.DisposeAsync();

    await Log.CloseAndFlushAsync();
}
