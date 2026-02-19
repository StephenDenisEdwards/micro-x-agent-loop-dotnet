using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Web;

public class WebSearchTool : ITool
{
    private const int DefaultCount = 5;

    private readonly ISearchProvider _provider;

    public WebSearchTool(ISearchProvider provider)
    {
        _provider = provider;
    }

    public string Name => "web_search";

    public string Description =>
        "Search the web and return a list of results with titles, URLs, " +
        "and descriptions. Use this to discover URLs before fetching " +
        "their full content with web_fetch.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Search query (max 400 characters)"
                },
                "count": {
                    "type": "number",
                    "description": "Number of results to return (1-20, default 5)"
                }
            },
            "required": ["query"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        var query = input["query"]?.GetValue<string>()?.Trim() ?? "";
        if (string.IsNullOrEmpty(query))
            return "Error: query must not be empty";

        if (query.Length > 400)
            query = query[..400];

        var count = input["count"] is not null
            ? Math.Clamp(input["count"]!.GetValue<int>(), 1, 20)
            : DefaultCount;

        List<SearchResult> results;
        try
        {
            results = await _provider.SearchAsync(query, count);
        }
        catch (TaskCanceledException)
        {
            return "Error: Search request timed out";
        }
        catch (HttpRequestException ex)
        {
            return $"Error: {ex.Message}";
        }

        if (results.Count == 0)
            return $"No results found for: {query}";

        var lines = new List<string>
        {
            $"Search: \"{query}\"",
            $"Results: {results.Count}",
            "",
        };

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            lines.Add($"{i + 1}. {r.Title}");
            lines.Add($"   {r.Url}");
            if (!string.IsNullOrEmpty(r.Description))
                lines.Add($"   {r.Description}");
            lines.Add("");
        }

        return string.Join("\n", lines).TrimEnd();
    }
}
