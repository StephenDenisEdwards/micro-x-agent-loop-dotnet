using Microsoft.Extensions.Configuration;
using MicroXAgentLoop;
using MicroXAgentLoop.Tools;

DotNetEnv.Env.Load();

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

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
var documentsDirectory = configuration["DocumentsDirectory"];
var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
var googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");

var tools = ToolRegistry.GetAll(documentsDirectory, googleClientId, googleClientSecret);

var agent = new Agent(new AgentConfig(
    Model: model,
    MaxTokens: maxTokens,
    Temperature: temperature,
    ApiKey: apiKey,
    Tools: tools,
    SystemPrompt: SystemPrompt.Text,
    MaxToolResultChars: maxToolResultChars,
    MaxConversationMessages: maxConversationMessages));

Console.WriteLine("micro-x-agent-loop (type 'exit' to quit)");
Console.WriteLine($"Tools: {string.Join(", ", tools.Select(t => t.Name))}");
Console.WriteLine();

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
        Console.Write("\nassistant> ");
        await agent.RunAsync(trimmed);
        Console.WriteLine("\n");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\nError: {ex.Message}\n");
    }
}
