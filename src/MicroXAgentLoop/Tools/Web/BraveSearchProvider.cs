using System.Text.Json;
using MicroXAgentLoop.Tools;

namespace MicroXAgentLoop.Tools.Web;

public class BraveSearchProvider : ISearchProvider
{
    private const string BraveSearchUrl = "https://api.search.brave.com/res/v1/web/search";

    private readonly string _apiKey;

    private static HttpClient Http => HttpClientFactory.Api;

    public BraveSearchProvider(string apiKey)
    {
        _apiKey = apiKey;
    }

    public string ProviderName => "Brave";

    public async Task<List<SearchResult>> SearchAsync(string query, int count, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BraveSearchUrl}?q={Uri.EscapeDataString(query)}&count={count}");
        request.Headers.Add("X-Subscription-Token", _apiKey);
        request.Headers.Add("Accept", "application/json");

        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} from Brave Search API");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var results = new List<SearchResult>();
        if (root.TryGetProperty("web", out var web)
            && web.TryGetProperty("results", out var rawResults))
        {
            foreach (var r in rawResults.EnumerateArray())
            {
                results.Add(new SearchResult(
                    Title: r.TryGetProperty("title", out var t) ? t.GetString() ?? "(no title)" : "(no title)",
                    Url: r.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                    Description: r.TryGetProperty("description", out var d) ? d.GetString() ?? "" : ""));
            }
        }

        return results;
    }
}
