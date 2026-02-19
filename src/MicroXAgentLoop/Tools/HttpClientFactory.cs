namespace MicroXAgentLoop.Tools;

/// <summary>
/// Shared HttpClient instances for tool implementations.
/// Each client is configured for its target API pattern.
/// </summary>
public static class HttpClientFactory
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// General-purpose client with browser-like headers for scraping HTML pages.
    /// </summary>
    public static HttpClient Browser { get; } = CreateBrowserClient();

    /// <summary>
    /// JSON API client with a standard timeout but no browser headers.
    /// </summary>
    public static HttpClient Api { get; } = new()
    {
        Timeout = DefaultTimeout,
    };

    private static HttpClient CreateBrowserClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        };

        return new HttpClient(handler)
        {
            Timeout = DefaultTimeout,
            DefaultRequestHeaders =
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
                { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
                { "Accept-Language", "en-US,en;q=0.5" },
            },
        };
    }
}
