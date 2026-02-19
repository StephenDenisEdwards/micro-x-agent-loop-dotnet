using System.Text.Json.Nodes;

namespace MicroXAgentLoop;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonNode InputSchema { get; }
    Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default);
}
