using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools;

public class AppendFileTool : FileToolBase
{
    public AppendFileTool(string? workingDirectory = null) : base(workingDirectory) { }

    public override string Name => "append_file";

    public override string Description =>
        "Append content to the end of a file. " +
        "The file must already exist. " +
        "Use this to write large files in stages — " +
        "create the file with write_file first, then append additional sections.";

    public override JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "Absolute or relative path to the file to append to"
                },
                "content": {
                    "type": "string",
                    "description": "The content to append to the file"
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

            if (!File.Exists(path))
                return $"Error: file does not exist: {path}. Use write_file to create it first.";

            await File.AppendAllTextAsync(path, content, ct);
            return $"Successfully appended to {path}";
        }
        catch (Exception ex)
        {
            return $"Error appending to file: {ex.Message}";
        }
    }
}
