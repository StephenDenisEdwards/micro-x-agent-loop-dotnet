using System.Text;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Polly;
using Polly.Retry;

namespace MicroXAgentLoop;

public class SummarizeCompactionStrategy : ICompactionStrategy
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly int _thresholdTokens;
    private readonly int _protectedTailMessages;

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

    private const string SummarizePrompt =
        """
        Summarize the following conversation history between a user and an AI assistant.
        Preserve these details precisely:
        - The original user request and any specific criteria or instructions
        - All decisions made and their reasoning
        - Key data points, URLs, file paths, and identifiers that may be needed later
        - Any scores, rankings, or evaluations produced
        - Current task status and next steps

        Do NOT include raw tool output data (job descriptions, email bodies, etc.) —
        just note what was retrieved and key findings.

        Format as a concise narrative summary.

        ---
        CONVERSATION HISTORY:

        """;

    public SummarizeCompactionStrategy(
        AnthropicClient client,
        string model,
        int thresholdTokens = 80_000,
        int protectedTailMessages = 6)
    {
        _client = client;
        _model = model;
        _thresholdTokens = thresholdTokens;
        _protectedTailMessages = protectedTailMessages;
    }

    public async Task<List<Message>> MaybeCompactAsync(List<Message> messages)
    {
        var estimated = EstimateTokens(messages);
        if (estimated < _thresholdTokens)
            return messages;

        if (messages.Count < 2)
            return messages;

        var compactStart = 1;
        var compactEnd = messages.Count - _protectedTailMessages;

        if (compactEnd <= compactStart)
            return messages;

        compactEnd = AdjustBoundary(messages, compactStart, compactEnd);

        if (compactEnd <= compactStart)
            return messages;

        var compactable = messages.GetRange(compactStart, compactEnd - compactStart);

        Console.Error.WriteLine(
            $"  Compaction: estimated ~{estimated:N0} tokens, threshold {_thresholdTokens:N0}" +
            $" — compacting {compactable.Count} messages");

        string summary;
        try
        {
            summary = await SummarizeAsync(compactable);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"  Warning: Compaction failed: {ex.Message}. Falling back to history trimming.");
            return messages;
        }

        var result = RebuildMessages(messages, compactEnd, summary);

        var summaryTokens = summary.Length / 4;
        var freed = estimated - EstimateTokens(result);
        Console.Error.WriteLine(
            $"  Compaction: summarized {compactable.Count} messages into ~{summaryTokens:N0} tokens," +
            $" freed ~{freed:N0} estimated tokens");

        return result;
    }

    private static int EstimateTokens(List<Message> messages)
    {
        var totalChars = 0;
        foreach (var msg in messages)
        {
            if (msg.Content is null) continue;
            foreach (var block in msg.Content)
            {
                switch (block)
                {
                    case TextContent text:
                        totalChars += text.Text?.Length ?? 0;
                        break;
                    case ToolUseContent toolUse:
                        totalChars += toolUse.Name?.Length ?? 0;
                        totalChars += JsonSerializer.Serialize(toolUse.Input).Length;
                        break;
                    case ToolResultContent toolResult:
                        if (toolResult.Content is not null)
                            foreach (var sub in toolResult.Content)
                                if (sub is TextContent subText)
                                    totalChars += subText.Text?.Length ?? 0;
                        break;
                }
            }
        }
        return totalChars / 4;
    }

    private static string FormatForSummarization(List<Message> messages)
    {
        var parts = new List<string>();
        foreach (var msg in messages)
        {
            var role = msg.Role.ToString().ToLower();
            if (msg.Content is null) continue;

            var blockTexts = new List<string>();
            foreach (var block in msg.Content)
            {
                switch (block)
                {
                    case TextContent text:
                        blockTexts.Add(text.Text ?? "");
                        break;
                    case ToolUseContent toolUse:
                        var inp = JsonSerializer.Serialize(toolUse.Input);
                        if (inp.Length > 200)
                            inp = inp[..200] + "...";
                        blockTexts.Add($"[Tool call: {toolUse.Name}({inp})]");
                        break;
                    case ToolResultContent toolResult:
                        var resultText = new StringBuilder();
                        if (toolResult.Content is not null)
                            foreach (var sub in toolResult.Content)
                                if (sub is TextContent subText)
                                    resultText.Append(subText.Text ?? "");
                        var preview = PreviewText(resultText.ToString());
                        blockTexts.Add($"[Tool result ({toolResult.ToolUseId})]: {preview}");
                        break;
                }
            }
            parts.Add($"[{role}]: " + string.Join("\n", blockTexts));
        }
        return string.Join("\n\n", parts);
    }

    private static string PreviewText(string text)
    {
        if (text.Length <= 700)
            return text;
        return text[..500] + "\n[...truncated...]\n" + text[^200..];
    }

    private async Task<string> SummarizeAsync(List<Message> compactable)
    {
        var formatted = FormatForSummarization(compactable);

        // Cap summarization input
        if (formatted.Length > 100_000)
        {
            const int half = 50_000;
            formatted =
                formatted[..half]
                + "\n\n[...middle of conversation omitted for brevity...]\n\n"
                + formatted[^half..];
        }

        MessageResponse? response = null;
        await RetryPipeline.ExecuteAsync(async _ =>
        {
            var parameters = new MessageParameters
            {
                Model = _model,
                MaxTokens = 4096,
                Temperature = 0,
                Messages = [new Message(RoleType.User, SummarizePrompt + formatted)],
            };

            response = await _client.Messages.GetClaudeMessageAsync(parameters);
        });

        return response!.Content.OfType<TextContent>().First().Text!;
    }

    private static int AdjustBoundary(List<Message> messages, int start, int end)
    {
        while (end > start + 1)
        {
            var boundaryMsg = messages[end - 1];
            if (boundaryMsg.Role != RoleType.Assistant)
                break;

            if (boundaryMsg.Content is null)
                break;

            var hasToolUse = boundaryMsg.Content.OfType<ToolUseContent>().Any();
            if (!hasToolUse)
                break;

            // This assistant message has tool_use — its tool_result is at messages[end],
            // which would be in the protected tail. Pull boundary back.
            end--;
        }
        return end;
    }

    private static List<Message> RebuildMessages(List<Message> messages, int compactEnd, string summary)
    {
        var firstMsg = messages[0];
        var originalContent = "";
        if (firstMsg.Content is not null)
        {
            var textParts = firstMsg.Content
                .OfType<TextContent>()
                .Select(t => t.Text ?? "");
            originalContent = string.Join("\n", textParts);
        }

        var mergedContent = originalContent
            + "\n\n[CONTEXT SUMMARY]\n"
            + summary
            + "\n[END CONTEXT SUMMARY]";

        var mergedFirst = new Message(RoleType.User, mergedContent);

        var tail = messages.GetRange(compactEnd, messages.Count - compactEnd);

        var result = new List<Message> { mergedFirst };

        // Insert assistant ack if needed for role alternation
        if (tail.Count > 0 && tail[0].Role == RoleType.User)
        {
            result.Add(new Message
            {
                Role = RoleType.Assistant,
                Content = [new TextContent { Text = "Understood. Continuing with the current task." }],
            });
        }

        result.AddRange(tail);
        return result;
    }
}
