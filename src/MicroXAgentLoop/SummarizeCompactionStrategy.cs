using System.Text;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Polly;
using Serilog;

namespace MicroXAgentLoop;

public class SummarizeCompactionStrategy : ICompactionStrategy
{
    /// <summary>Approximate chars-per-token ratio for estimation.</summary>
    private const int CharsPerToken = 4;

    /// <summary>Max chars to show when previewing a tool input in the summary.</summary>
    private const int ToolInputPreviewChars = 200;

    /// <summary>Max chars to show when previewing a tool result in the summary.</summary>
    private const int ToolResultPreviewChars = 700;

    /// <summary>Head portion of tool result preview.</summary>
    private const int ToolResultHeadChars = 500;

    /// <summary>Tail portion of tool result preview.</summary>
    private const int ToolResultTailChars = 200;

    /// <summary>Cap on total chars sent to the summarization model.</summary>
    private const int MaxSummarizationInputChars = 100_000;

    /// <summary>Max tokens for the summarization response.</summary>
    private const int SummarizationMaxTokens = 4096;

    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly int _thresholdTokens;
    private readonly int _protectedTailMessages;

    private static readonly ResiliencePipeline RetryPipeline = RetryPipelineFactory.Create();

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

    public async Task<List<Message>> MaybeCompactAsync(List<Message> messages, CancellationToken ct = default)
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

        Log.Information(
            "Compaction: estimated ~{Estimated:N0} tokens, threshold {Threshold:N0} — compacting {Count} messages",
            estimated, _thresholdTokens, compactable.Count);

        string summary;
        try
        {
            summary = await SummarizeAsync(compactable, ct);
        }
        catch (Exception ex)
        {
            Log.Warning("Compaction failed: {Message}. Falling back to history trimming.", ex.Message);
            return messages;
        }

        var result = RebuildMessages(messages, compactEnd, summary);

        var summaryTokens = summary.Length / CharsPerToken;
        var freed = estimated - EstimateTokens(result);
        Log.Information(
            "Compaction: summarized {Count} messages into ~{SummaryTokens:N0} tokens, freed ~{Freed:N0} estimated tokens",
            compactable.Count, summaryTokens, freed);

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
        return totalChars / CharsPerToken;
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
                        if (inp.Length > ToolInputPreviewChars)
                            inp = inp[..ToolInputPreviewChars] + "...";
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
        if (text.Length <= ToolResultPreviewChars)
            return text;
        return text[..ToolResultHeadChars] + "\n[...truncated...]\n" + text[^ToolResultTailChars..];
    }

    private async Task<string> SummarizeAsync(List<Message> compactable, CancellationToken ct)
    {
        var formatted = FormatForSummarization(compactable);

        // Cap summarization input
        if (formatted.Length > MaxSummarizationInputChars)
        {
            var half = MaxSummarizationInputChars / 2;
            formatted =
                formatted[..half]
                + "\n\n[...middle of conversation omitted for brevity...]\n\n"
                + formatted[^half..];
        }

        MessageResponse? response = null;
        await RetryPipeline.ExecuteAsync(async token =>
        {
            var parameters = new MessageParameters
            {
                Model = _model,
                MaxTokens = SummarizationMaxTokens,
                Temperature = 0,
                Messages = [new Message(RoleType.User, SummarizePrompt + formatted)],
            };

            response = await _client.Messages.GetClaudeMessageAsync(parameters, token);
        }, ct);

        if (response is null)
            throw new InvalidOperationException("Summarization API returned no response after retries.");

        return response.Content.OfType<TextContent>().First().Text!;
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
