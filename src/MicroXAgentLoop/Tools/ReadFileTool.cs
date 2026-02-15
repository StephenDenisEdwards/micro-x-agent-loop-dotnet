using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MicroXAgentLoop.Tools;

public class ReadFileTool : ITool
{
    private readonly string? _documentsDirectory;

    public ReadFileTool(string? documentsDirectory = null)
    {
        _documentsDirectory = documentsDirectory;
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
            if (!Path.IsPathRooted(filePath) && !File.Exists(filePath))
                filePath = ResolveRelativePath(filePath) ?? filePath;

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

    /// <summary>
    /// Walks up from CWD to the repo root, trying to resolve a relative path at each level.
    /// Falls back to the configured documents directory if set.
    /// </summary>
    private string? ResolveRelativePath(string relativePath)
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
                return candidate;

            // Stop at repo root
            if (Directory.Exists(Path.Combine(dir, ".git")))
                break;

            dir = Directory.GetParent(dir)?.FullName;
        }

        // Try the configured documents directory as a fallback
        if (_documentsDirectory is not null)
        {
            var docsBase = Path.IsPathRooted(_documentsDirectory)
                ? _documentsDirectory
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), _documentsDirectory));

            var candidate = Path.Combine(docsBase, relativePath);
            if (File.Exists(candidate))
                return candidate;

            // Also try just the filename in case the relative path includes a directory prefix
            var fileName = Path.GetFileName(relativePath);
            candidate = Path.Combine(docsBase, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
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
