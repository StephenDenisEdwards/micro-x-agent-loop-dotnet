using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools;

public class AppendFileTool : ITool
{
    private readonly string? _workingDirectory;

    public AppendFileTool(string? workingDirectory = null)
    {
        _workingDirectory = workingDirectory;
    }

    public string Name => "append_file";

    public string Description =>
        "Append content to the end of a file. " +
        "The file must already exist. " +
        "Use this to write large files in stages — " +
        "create the file with write_file first, then append additional sections.";

    public JsonNode InputSchema => JsonNode.Parse("""
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

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        var path = input["path"]!.GetValue<string>();
        var content = input["content"]!.GetValue<string>();
        try
        {
            if (!Path.IsPathRooted(path) && _workingDirectory is not null)
                path = Path.Combine(_workingDirectory, path);

            if (!File.Exists(path))
                return $"Error: file does not exist: {path}. Use write_file to create it first.";

            await File.AppendAllTextAsync(path, content);
            return $"Successfully appended to {path}";
        }
        catch (Exception ex)
        {
            return $"Error appending to file: {ex.Message}";
        }
    }
}
