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
    private readonly string _systemPrompt;
    private readonly List<Message> _messages = [];
    private readonly Dictionary<string, ITool> _toolMap;
    private readonly List<CommonTool> _anthropicTools;

    public Agent(AgentConfig config)
    {
        _client = LlmClient.CreateClient(config.ApiKey);
        _model = config.Model;
        _maxTokens = config.MaxTokens;
        _systemPrompt = config.SystemPrompt;
        _toolMap = config.Tools.ToDictionary(t => t.Name);
        _anthropicTools = LlmClient.ToAnthropicTools(config.Tools);
    }

    public async Task<string> RunAsync(string userMessage)
    {
        _messages.Add(new Message(RoleType.User, userMessage));

        while (true)
        {
            var response = await LlmClient.ChatAsync(
                _client, _model, _maxTokens, _systemPrompt, _messages, _anthropicTools);

            _messages.Add(response.Message);

            if (response.StopReason != "tool_use")
            {
                return string.Join("\n",
                    response.Content.OfType<TextContent>().Select(b => b.Text));
            }

            var toolResults = new List<ContentBase>();

            foreach (var block in response.Content.OfType<ToolUseContent>())
            {
                if (!_toolMap.TryGetValue(block.Name, out var tool))
                {
                    toolResults.Add(new ToolResultContent
                    {
                        ToolUseId = block.Id,
                        Content = [new TextContent { Text = $"Error: unknown tool \"{block.Name}\"" }],
                        IsError = true,
                    });
                    continue;
                }

                try
                {
                    var result = await tool.ExecuteAsync(block.Input);
                    toolResults.Add(new ToolResultContent
                    {
                        ToolUseId = block.Id,
                        Content = [new TextContent { Text = result }],
                    });
                }
                catch (Exception ex)
                {
                    toolResults.Add(new ToolResultContent
                    {
                        ToolUseId = block.Id,
                        Content = [new TextContent { Text = $"Error executing tool \"{block.Name}\": {ex.Message}" }],
                        IsError = true,
                    });
                }
            }

            _messages.Add(new Message { Role = RoleType.User, Content = toolResults });
        }
    }
}
