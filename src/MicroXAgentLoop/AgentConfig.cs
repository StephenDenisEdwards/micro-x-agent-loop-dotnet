namespace MicroXAgentLoop;

public record AgentConfig(
    string Model,
    int MaxTokens,
    decimal Temperature,
    string ApiKey,
    IReadOnlyList<ITool> Tools,
    string SystemPrompt,
    int MaxToolResultChars = 40_000,
    int MaxConversationMessages = 50);
