namespace MicroXAgentLoop.Tools.Web;

public record SearchResult(string Title, string Url, string Description);

public interface ISearchProvider
{
    string ProviderName { get; }
    Task<List<SearchResult>> SearchAsync(string query, int count);
}
