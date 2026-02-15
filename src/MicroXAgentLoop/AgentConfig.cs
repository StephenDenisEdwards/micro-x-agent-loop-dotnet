namespace MicroXAgentLoop;

public record AgentConfig(
    string Model,
    int MaxTokens,
    string ApiKey,
    IReadOnlyList<ITool> Tools,
    string SystemPrompt);
