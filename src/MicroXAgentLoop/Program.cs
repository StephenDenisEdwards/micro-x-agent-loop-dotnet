using MicroXAgentLoop;
using MicroXAgentLoop.Tools;

DotNetEnv.Env.Load();

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("Error: ANTHROPIC_API_KEY environment variable is required.");
    Environment.Exit(1);
}

var tools = ToolRegistry.GetAll();

var agent = new Agent(new AgentConfig(
    Model: "claude-sonnet-4-5-20250929",
    MaxTokens: 8192,
    ApiKey: apiKey,
    Tools: tools,
    SystemPrompt: SystemPrompt.Text));

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
        var response = await agent.RunAsync(trimmed);
        Console.WriteLine($"\nassistant> {response}\n");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\nError: {ex.Message}\n");
    }
}
