using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Serilog;
using CommonTool = Anthropic.SDK.Common.Tool;

namespace MicroXAgentLoop;

public class Agent
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly decimal _temperature;
    private readonly string _systemPrompt;
    private readonly List<Message> _messages = [];
    private readonly Dictionary<string, ITool> _toolMap;
    private readonly List<CommonTool> _anthropicTools;
    private readonly int _maxToolResultChars;
    private readonly int _maxConversationMessages;
    private readonly ICompactionStrategy _compactionStrategy;

    public Agent(AgentConfig config)
    {
        _client = LlmClient.CreateClient(config.ApiKey);
        _model = config.Model;
        _maxTokens = config.MaxTokens;
        _temperature = config.Temperature;
        _systemPrompt = config.SystemPrompt;
        _toolMap = config.Tools.ToDictionary(t => t.Name);
        _anthropicTools = LlmClient.ToAnthropicTools(config.Tools);
        _maxToolResultChars = config.MaxToolResultChars;
        _maxConversationMessages = config.MaxConversationMessages;
        _compactionStrategy = config.CompactionStrategy ?? new NoneCompactionStrategy();
    }

    private const int MaxTokensRetries = 3;

    public async Task RunAsync(string userMessage, CancellationToken ct = default)
    {
        _messages.Add(new Message(RoleType.User, userMessage));
        await MaybeCompactAsync(ct);

        var maxTokensAttempts = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (message, toolUseBlocks, stopReason) = await LlmClient.StreamChatAsync(
                _client, _model, _maxTokens, _temperature, _systemPrompt, _messages, _anthropicTools, ct);

            _messages.Add(message);

            if (stopReason == "max_tokens" && toolUseBlocks.Count == 0)
            {
                maxTokensAttempts++;
                if (maxTokensAttempts >= MaxTokensRetries)
                {
                    Console.WriteLine(
                        $"\nassistant> [Stopped: response exceeded max_tokens " +
                        $"({_maxTokens}) {MaxTokensRetries} times in a row. " +
                        $"Try increasing MaxTokens in appsettings.json or simplifying the request.]");
                    return;
                }
                _messages.Add(new Message(RoleType.User,
                    "Your response was cut off because it exceeded the token limit. " +
                    "Please continue, but be more concise. If you were writing a file, " +
                    "break it into smaller sections or shorten the content."));
                Console.WriteLine();
                continue;
            }

            maxTokensAttempts = 0;

            if (toolUseBlocks.Count == 0)
                return;

            var toolResults = await ExecuteToolsAsync(toolUseBlocks, ct);
            _messages.Add(new Message { Role = RoleType.User, Content = toolResults });
            await MaybeCompactAsync(ct);

            Console.WriteLine();
        }
    }

    private async Task MaybeCompactAsync(CancellationToken ct)
    {
        var compacted = await _compactionStrategy.MaybeCompactAsync(_messages, ct);
        if (!ReferenceEquals(compacted, _messages))
        {
            _messages.Clear();
            _messages.AddRange(compacted);
        }
        TrimConversationHistory();
    }

    private async Task<List<ContentBase>> ExecuteToolsAsync(List<ToolUseContent> toolUseBlocks, CancellationToken ct)
    {
        var tasks = toolUseBlocks.Select(async block =>
        {
            if (!_toolMap.TryGetValue(block.Name, out var tool))
            {
                return new ToolResultContent
                {
                    ToolUseId = block.Id,
                    Content = [new TextContent { Text = $"Error: unknown tool \"{block.Name}\"" }],
                    IsError = true,
                };
            }

            try
            {
                var result = await tool.ExecuteAsync(block.Input, ct);
                result = TruncateToolResult(result, block.Name);
                return new ToolResultContent
                {
                    ToolUseId = block.Id,
                    Content = [new TextContent { Text = result }],
                };
            }
            catch (Exception ex)
            {
                return (ToolResultContent)new ToolResultContent
                {
                    ToolUseId = block.Id,
                    Content = [new TextContent { Text = $"Error executing tool \"{block.Name}\": {ex.Message}" }],
                    IsError = true,
                };
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Cast<ContentBase>().ToList();
    }

    private string TruncateToolResult(string result, string toolName)
    {
        if (_maxToolResultChars <= 0 || result.Length <= _maxToolResultChars)
            return result;

        var originalLength = result.Length;
        var truncated = result[.._maxToolResultChars];
        var message = $"\n\n[OUTPUT TRUNCATED: Showing {_maxToolResultChars:N0} of {originalLength:N0} characters from {toolName}]";
        Log.Warning("{ToolName} output truncated from {Original:N0} to {Max:N0} chars", toolName, originalLength, _maxToolResultChars);
        return truncated + message;
    }

    private void TrimConversationHistory()
    {
        if (_maxConversationMessages <= 0 || _messages.Count <= _maxConversationMessages)
            return;

        var removeCount = _messages.Count - _maxConversationMessages;
        // Remove from the start (oldest messages) but keep at least the most recent exchange
        if (removeCount > 0)
        {
            Log.Information(
                "Conversation history trimmed — removed {Count} oldest message(s) to stay within {Max} message limit",
                removeCount, _maxConversationMessages);
            _messages.RemoveRange(0, removeCount);
        }
    }
}
