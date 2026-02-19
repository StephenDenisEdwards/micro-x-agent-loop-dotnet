using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MicroXAgentLoop.Tools;

public class ReadFileTool : FileToolBase
{
    public ReadFileTool(string? workingDirectory = null) : base(workingDirectory) { }

    public override string Name => "read_file";

    public override string Description =>
        "Read the contents of a file and return it as text. Supports plain text files and .docx documents.";

    public override JsonNode InputSchema => JsonNode.Parse("""
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

    public override async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        var filePath = input["path"]!.GetValue<string>();
        try
        {
            filePath = ResolvePath(filePath);

            if (Path.GetExtension(filePath).Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractDocxText(filePath);
            }

            return await File.ReadAllTextAsync(filePath, ct);
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
