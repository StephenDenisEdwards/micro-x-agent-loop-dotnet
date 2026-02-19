using System.Text.Json.Nodes;
using Serilog;

namespace MicroXAgentLoop.Tools;

/// <summary>
/// Base class for tools that require Google OAuth credentials.
/// Provides shared credential storage and error handling.
/// </summary>
public abstract class GoogleToolBase : ITool
{
    protected readonly string GoogleClientId;
    protected readonly string GoogleClientSecret;

    protected GoogleToolBase(string googleClientId, string googleClientSecret)
    {
        GoogleClientId = googleClientId;
        GoogleClientSecret = googleClientSecret;
    }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonNode InputSchema { get; }
    public abstract Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default);

    protected string HandleError(string message)
    {
        Log.Error("{ToolName} error: {Message}", Name, message);
        return $"Error in {Name}: {message}";
    }
}
