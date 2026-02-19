using Microsoft.Extensions.Configuration;
using MicroXAgentLoop;
using MicroXAgentLoop.Mcp;
using MicroXAgentLoop.Tools;
using Serilog;
using AppConfig = MicroXAgentLoop.ConfigLoader.AppConfig;

DotNetEnv.Env.Load();

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var logDescriptions = LoggingConfig.SetupLogging(configuration);

AppConfig config;
try
{
    config = ConfigLoader.Load(configuration);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
    return;
}

// Assemble tools
var tools = ToolRegistry.GetAll(
    config.WorkingDirectory, config.GoogleClientId, config.GoogleClientSecret,
    config.AnthropicAdminApiKey, config.BraveApiKey);
var allTools = new List<ITool>(tools);

// Connect to MCP servers
McpManager? mcpManager = null;
var mcpTools = new List<ITool>();

if (config.McpServers.Count > 0)
{
    mcpManager = new McpManager(config.McpServers);
    mcpTools = await mcpManager.ConnectAllAsync();
    allTools.AddRange(mcpTools);
}

// Configure compaction
ICompactionStrategy? compactionStrategy = config.CompactionStrategy.ToLowerInvariant() switch
{
    "summarize" => new SummarizeCompactionStrategy(
        LlmClient.CreateClient(config.ApiKey),
        config.Model,
        config.CompactionThresholdTokens,
        config.ProtectedTailMessages),
    _ => null,
};

var agent = new Agent(new AgentConfig(
    Model: config.Model,
    MaxTokens: config.MaxTokens,
    Temperature: config.Temperature,
    ApiKey: config.ApiKey,
    Tools: allTools,
    SystemPrompt: SystemPrompt.GetText(),
    MaxToolResultChars: config.MaxToolResultChars,
    MaxConversationMessages: config.MaxConversationMessages,
    CompactionStrategy: compactionStrategy));

StartupDisplay.Show(
    allTools, mcpTools, config.WorkingDirectory,
    config.CompactionStrategy, config.CompactionThresholdTokens,
    config.ProtectedTailMessages, logDescriptions);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    while (!cts.Token.IsCancellationRequested)
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
            await agent.RunAsync(trimmed, cts.Token);
            Console.WriteLine("\n");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n[Cancelled]");
            break;
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
