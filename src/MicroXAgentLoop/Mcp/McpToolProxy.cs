using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Polly;
using Polly.Retry;
using Serilog;

namespace MicroXAgentLoop.Mcp;

/// <summary>
/// Adapter that wraps an MCP tool definition + client into an ITool implementation.
/// </summary>
public class McpToolProxy : ITool
{
    private const int MaxRetryAttempts = 2;

    private static readonly ResiliencePipeline RetryPipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    Log.Warning(
                        "MCP tool call failed: {Error}. Retrying in {Delay}s (attempt {Attempt}/{Max})...",
                        args.Outcome.Exception?.Message, args.RetryDelay.TotalSeconds,
                        args.AttemptNumber + 1, MaxRetryAttempts);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();

    private readonly string _serverName;
    private readonly McpClientTool _mcpTool;
    private readonly McpClient _client;
    private readonly JsonNode _inputSchema;

    public McpToolProxy(string serverName, McpClientTool mcpTool, McpClient client)
    {
        _serverName = serverName;
        _mcpTool = mcpTool;
        _client = client;
        _inputSchema = JsonNode.Parse(mcpTool.JsonSchema.GetRawText()) ?? new JsonObject();
    }

    public string Name => $"{_serverName}__{_mcpTool.Name}";

    public string Description => _mcpTool.Description ?? "";

    public JsonNode InputSchema => _inputSchema;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        Log.Debug("MCP tool call: {Name} | input: {Input}", Name, input.ToJsonString());

        var arguments = new Dictionary<string, object?>();
        if (input is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                arguments[kvp.Key] = kvp.Value is not null
                    ? JsonSerializer.Deserialize<object>(kvp.Value.ToJsonString())
                    : null;
            }
        }

        var output = await RetryPipeline.ExecuteAsync(async _ =>
        {
            var result = await _client.CallToolAsync(_mcpTool.Name, arguments);

            Log.Debug(
                "MCP raw response: {Name} | isError={IsError} | blocks={Count}",
                Name, result.IsError, result.Content.Count);

            var textParts = result.Content
                .OfType<TextContentBlock>()
                .Select(b => b.Text)
                .ToList();

            var text = textParts.Count > 0 ? string.Join("\n", textParts) : "(no output)";

            if (result.IsError == true)
            {
                Log.Warning("MCP tool error: {Name} | result: {Output}", Name, text[..Math.Min(500, text.Length)]);
                throw new InvalidOperationException(text);
            }

            return text;
        });

        Log.Debug("MCP tool result: {Name} | chars={Chars}", Name, output.Length);
        return output;
    }
}
