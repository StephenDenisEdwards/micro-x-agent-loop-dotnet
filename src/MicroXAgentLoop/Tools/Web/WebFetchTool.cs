using System.Text.Json;
using System.Text.Json.Nodes;
using HtmlAgilityPack;

namespace MicroXAgentLoop.Tools.Web;

public class WebFetchTool : ITool
{
    private const int DefaultMaxChars = 50_000;
    private const int MaxResponseBytes = 2_000_000; // 2 MB
    private const int TimeoutSeconds = 30;
    private const int MaxRedirects = 5;

    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = MaxRedirects,
    })
    {
        Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
            { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
            { "Accept-Language", "en-US,en;q=0.5" },
        },
    };

    public string Name => "web_fetch";

    public string Description =>
        "Fetch content from a URL and return it as readable text. " +
        "Supports HTML pages (converted to plain text with links preserved), " +
        "JSON APIs (pretty-printed), and plain text. GET requests only.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "The HTTP or HTTPS URL to fetch"
                },
                "maxChars": {
                    "type": "number",
                    "description": "Maximum characters of content to return (default 50000). Content beyond this limit is truncated with a notice."
                }
            },
            "required": ["url"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        var url = input["url"]!.GetValue<string>();
        var maxChars = input["maxChars"] is not null
            ? input["maxChars"]!.GetValue<int>()
            : DefaultMaxChars;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return "Error: URL must use http or https scheme";
        }

        HttpResponseMessage response;
        try
        {
            response = await Http.GetAsync(uri);
        }
        catch (TaskCanceledException)
        {
            return $"Error: Request timed out after {TimeoutSeconds} seconds";
        }
        catch (HttpRequestException ex)
        {
            return $"Error: {ex.Message}";
        }

        if (!response.IsSuccessStatusCode)
        {
            return $"Error: HTTP {(int)response.StatusCode} fetching {url}";
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (bytes.Length > MaxResponseBytes)
        {
            return $"Error: Response too large ({bytes.Length:N0} bytes, max {MaxResponseBytes:N0} bytes)";
        }

        var body = await response.Content.ReadAsStringAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;

        string content;
        var title = "";

        if (contentType.Contains("text/html") || contentType.Contains("application/xhtml"))
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(body);
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            if (titleNode is not null)
                title = titleNode.InnerText.Trim();

            content = HtmlUtilities.HtmlToText(body);
        }
        else if (contentType.Contains("application/json"))
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(body);
                content = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                content = body;
            }
        }
        else
        {
            content = body;
        }

        var originalLength = content.Length;
        var truncated = false;
        if (originalLength > maxChars)
        {
            content = content[..maxChars];
            truncated = true;
        }

        var parts = new List<string> { $"URL: {url}" };
        if (finalUrl != url)
            parts.Add($"Final URL: {finalUrl}");
        parts.Add($"Status: {(int)response.StatusCode}");
        parts.Add($"Content-Type: {contentType}");
        if (!string.IsNullOrEmpty(title))
            parts.Add($"Title: {title}");

        var lengthStr = truncated
            ? $"{maxChars:N0} chars (truncated from {originalLength:N0})"
            : $"{originalLength:N0} chars";
        parts.Add($"Length: {lengthStr}");

        parts.Add("");
        parts.Add("--- Content ---");
        parts.Add("");
        parts.Add(content);

        if (truncated)
        {
            parts.Add("");
            parts.Add($"[Content truncated at {maxChars:N0} characters]");
        }

        return string.Join("\n", parts);
    }
}
