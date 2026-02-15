using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools;

public class WriteFileTool : ITool
{
    public string Name => "write_file";
    public string Description => "Write content to a file, creating it if it doesn't exist.";

    public JsonNode InputSchema => JsonNode.Parse("""
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

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        var path = input["path"]!.GetValue<string>();
        var content = input["content"]!.GetValue<string>();
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(path, content);
            return $"Successfully wrote to {path}";
        }
        catch (Exception ex)
        {
            return $"Error writing file: {ex.Message}";
        }
    }
}
