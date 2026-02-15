using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
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
    }

    public async Task RunAsync(string userMessage)
    {
        _messages.Add(new Message(RoleType.User, userMessage));
        TrimConversationHistory();

        while (true)
        {
            var (message, toolUseBlocks) = await LlmClient.StreamChatAsync(
                _client, _model, _maxTokens, _temperature, _systemPrompt, _messages, _anthropicTools);

            _messages.Add(message);

            if (toolUseBlocks.Count == 0)
                return;

            var toolResults = await ExecuteToolsAsync(toolUseBlocks);
            _messages.Add(new Message { Role = RoleType.User, Content = toolResults });
            TrimConversationHistory();

            Console.Write("\nassistant> ");
        }
    }

    private async Task<List<ContentBase>> ExecuteToolsAsync(List<ToolUseContent> toolUseBlocks)
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
                var result = await tool.ExecuteAsync(block.Input);
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
        Console.Error.WriteLine($"  Warning: {toolName} output truncated from {originalLength:N0} to {_maxToolResultChars:N0} chars");
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
            Console.Error.WriteLine(
                $"  Note: Conversation history trimmed — removed {removeCount} oldest message(s) " +
                $"to stay within the {_maxConversationMessages} message limit");
            _messages.RemoveRange(0, removeCount);
        }
    }
}
