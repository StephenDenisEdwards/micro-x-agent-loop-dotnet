using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools;

/// <summary>
/// Base class for file-based tools that share working directory path resolution.
/// </summary>
public abstract class FileToolBase : ITool
{
    protected readonly string? _workingDirectory;

    protected FileToolBase(string? workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonNode InputSchema { get; }
    public abstract Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default);

    protected string ResolvePath(string path) =>
        !Path.IsPathRooted(path) && _workingDirectory is not null
            ? Path.Combine(_workingDirectory, path)
            : path;
}
