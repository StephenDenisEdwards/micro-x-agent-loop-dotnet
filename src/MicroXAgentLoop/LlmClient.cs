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
    private const string SpinnerFrames = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";
    private const string SpinnerLabel = " Thinking...";

    private sealed class Spinner
    {
        private readonly string _prefix;
        private readonly Thread _thread;
        private volatile bool _stopped;
        private readonly int _frameWidth = 1 + SpinnerLabel.Length;

        public Spinner(string prefix = "")
        {
            _prefix = prefix;
            _thread = new Thread(Run) { IsBackground = true };
        }

        public void Start() => _thread.Start();

        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            _thread.Join();
            var clear = _prefix + new string(' ', _frameWidth);
            Console.Write("\r" + clear + "\r" + _prefix);
        }

        private void Run()
        {
            var i = 0;
            try
            {
                while (!_stopped)
                {
                    var frame = SpinnerFrames[i % SpinnerFrames.Length] + SpinnerLabel;
                    Console.Write("\r" + _prefix + frame);
                    Thread.Sleep(80);
                    i++;
                }
            }
            catch (Exception)
            {
                // Terminal doesn't support these characters; fail silently
            }
        }
    }

    private static readonly ResiliencePipeline RetryPipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 5,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(10),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>(ex =>
                        ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    .Handle<HttpRequestException>(ex => ex.StatusCode is null) // connection error
                    .Handle<TaskCanceledException>(), // timeout
                OnRetry = args =>
                {
                    var reason = args.Outcome.Exception switch
                    {
                        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "Rate limited",
                        HttpRequestException => "Connection error",
                        TaskCanceledException => "Request timed out",
                        _ => args.Outcome.Exception?.GetType().Name ?? "Unknown error",
                    };
                    Console.Error.WriteLine(
                        $"{reason}. Retrying in {args.RetryDelay.TotalSeconds:F0}s " +
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
    /// Returns the assembled message, any tool use blocks found, and the stop reason.
    /// </summary>
    public static async Task<(Message Message, List<ToolUseContent> ToolUseBlocks, string StopReason)> StreamChatAsync(
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
        var spinner = new Spinner(prefix: "assistant> ");
        spinner.Start();
        var firstOutput = false;

        try
        {
            await RetryPipeline.ExecuteAsync(async ct =>
            {
                outputs.Clear();
                await foreach (var res in client.Messages.StreamClaudeMessageAsync(parameters, ct))
                {
                    if (res.Delta?.Text is not null)
                    {
                        if (!firstOutput)
                        {
                            spinner.Stop();
                            firstOutput = true;
                        }
                        Console.Write(res.Delta.Text);
                    }
                    outputs.Add(res);
                }
            });

            if (!firstOutput)
                spinner.Stop();
        }
        catch
        {
            spinner.Stop();
            throw;
        }

        // Build the full message from streamed outputs
        var message = new Message(outputs);
        var toolUseBlocks = message.Content?.OfType<ToolUseContent>().ToList() ?? [];

        // Extract token usage and stop reason from streamed outputs
        var inputTokens = outputs.FirstOrDefault()?.StreamStartMessage?.Usage?.InputTokens ?? 0;
        var outputTokens = outputs.LastOrDefault()?.Usage?.OutputTokens ?? 0;
        Console.Error.WriteLine($"  [{inputTokens} in / {outputTokens} out tokens]");

        var stopReason = outputs
            .Select(o => o.StopReason)
            .LastOrDefault(sr => !string.IsNullOrEmpty(sr)) ?? "";

        return (message, toolUseBlocks, stopReason);
    }
}
