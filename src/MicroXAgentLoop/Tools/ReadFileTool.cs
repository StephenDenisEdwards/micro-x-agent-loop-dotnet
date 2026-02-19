using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MicroXAgentLoop.Tools;

public class ReadFileTool : ITool
{
    private readonly string? _workingDirectory;

    public ReadFileTool(string? workingDirectory = null)
    {
        _workingDirectory = workingDirectory;
    }

    public string Name => "read_file";

    public string Description =>
        "Read the contents of a file and return it as text. Supports plain text files and .docx documents.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "Absolute or relative path to the file to read"
                }
            },
            "required": ["path"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        var filePath = input["path"]!.GetValue<string>();
        try
        {
            if (!Path.IsPathRooted(filePath) && _workingDirectory is not null)
                filePath = Path.Combine(_workingDirectory, filePath);

            if (Path.GetExtension(filePath).Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractDocxText(filePath);
            }

            return await File.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }

    private static string ExtractDocxText(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc?.MainDocumentPart?.Document?.Body;
        if (body is null)
            return string.Empty;

        var paragraphs = body.Descendants<Paragraph>()
            .Select(p => p.InnerText);
        return string.Join(Environment.NewLine, paragraphs);
    }
}
