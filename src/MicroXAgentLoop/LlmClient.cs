using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using Polly;
using Polly.Retry;
using CommonTool = Anthropic.SDK.Common.Tool;

namespace MicroXAgentLoop;

public static class LlmClient
{
    private static readonly ResiliencePipeline RetryPipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 5,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(10),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>(ex =>
                        ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests),
                OnRetry = args =>
                {
                    Console.Error.WriteLine(
                        $"Rate limited. Retrying in {args.RetryDelay.TotalSeconds:F0}s " +
                        $"(attempt {args.AttemptNumber + 1}/5)...");
                    return ValueTask.CompletedTask;
                },
            })
            .Build();

    public static AnthropicClient CreateClient(string apiKey)
    {
        return new AnthropicClient(new APIAuthentication(apiKey));
    }

    public static List<CommonTool> ToAnthropicTools(IReadOnlyList<ITool> tools)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        return tools.Select(t =>
        {
            var schemaJson = t.InputSchema.ToJsonString(jsonOptions);
            return (CommonTool)new Function(t.Name, t.Description, JsonNode.Parse(schemaJson));
        }).ToList();
    }

    /// <summary>
    /// Stream a chat response, printing text deltas to stdout in real time.
    /// Returns the assembled message and any tool use blocks found.
    /// </summary>
    public static async Task<(Message Message, List<ToolUseContent> ToolUseBlocks)> StreamChatAsync(
        AnthropicClient client,
        string model,
        int maxTokens,
        decimal temperature,
        string systemPrompt,
        List<Message> messages,
        List<CommonTool> tools)
    {
        var parameters = new MessageParameters
        {
            Messages = messages,
            MaxTokens = maxTokens,
            Model = model,
            Stream = true,
            Temperature = temperature,
            Tools = tools,
            System = [new SystemMessage(systemPrompt)],
        };

        var outputs = new List<MessageResponse>();

        await RetryPipeline.ExecuteAsync(async ct =>
        {
            outputs.Clear();
            await foreach (var res in client.Messages.StreamClaudeMessageAsync(parameters, ct))
            {
                if (res.Delta?.Text is not null)
                {
                    Console.Write(res.Delta.Text);
                }
                outputs.Add(res);
            }
        });

        // Build the full message from streamed outputs
        var message = new Message(outputs);
        var toolUseBlocks = message.Content?.OfType<ToolUseContent>().ToList() ?? [];

        return (message, toolUseBlocks);
    }
}
