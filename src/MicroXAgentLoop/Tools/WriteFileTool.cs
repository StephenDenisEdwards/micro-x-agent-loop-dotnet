using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools;

public class WriteFileTool : FileToolBase
{
    public WriteFileTool(string? workingDirectory = null) : base(workingDirectory) { }

    public override string Name => "write_file";
    public override string Description => "Write content to a file, creating it if it doesn't exist.";

    public override JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "Absolute or relative path to the file to write"
                },
                "content": {
                    "type": "string",
                    "description": "The content to write to the file"
                }
            },
            "required": ["path", "content"]
        }
        """)!;

    public override async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        var path = input["path"]!.GetValue<string>();
        var content = input["content"]!.GetValue<string>();
        try
        {
            path = ResolvePath(path);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(path, content, ct);
            return $"Successfully wrote to {path}";
        }
        catch (Exception ex)
        {
            return $"Error writing file: {ex.Message}";
        }
    }
}
