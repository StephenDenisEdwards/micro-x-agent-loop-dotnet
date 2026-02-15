using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using CommonTool = Anthropic.SDK.Common.Tool;

namespace MicroXAgentLoop;

public static class LlmClient
{
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

    public static async Task<MessageResponse> ChatAsync(
        AnthropicClient client,
        string model,
        int maxTokens,
        string systemPrompt,
        List<Message> messages,
        List<CommonTool> tools)
    {
        var parameters = new MessageParameters
        {
            Messages = messages,
            MaxTokens = maxTokens,
            Model = model,
            Stream = false,
            Temperature = 1.0m,
            Tools = tools,
            System = [new SystemMessage(systemPrompt)],
        };

        return await client.Messages.GetClaudeMessageAsync(parameters);
    }
}
