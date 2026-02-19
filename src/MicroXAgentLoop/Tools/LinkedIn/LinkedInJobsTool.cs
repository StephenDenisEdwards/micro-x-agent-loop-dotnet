using System.Text.Json.Nodes;
using System.Web;
using HtmlAgilityPack;
using MicroXAgentLoop.Tools;

namespace MicroXAgentLoop.Tools.LinkedIn;

public class LinkedInJobsTool : ITool
{
    private static HttpClient Http => HttpClientFactory.Browser;

    public string Name => "linkedin_jobs";

    public string Description =>
        "Search for job postings on LinkedIn. Returns job title, company, location, date, salary, and URL.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "keyword": {
                    "type": "string",
                    "description": "Job search keyword (e.g. 'software engineer')"
                },
                "location": {
                    "type": "string",
                    "description": "Job location (e.g. 'New York', 'Remote')"
                },
                "dateSincePosted": {
                    "type": "string",
                    "description": "Recency filter: 'past month', 'past week', or '24hr'"
                },
                "jobType": {
                    "type": "string",
                    "description": "Employment type: 'full time', 'part time', 'contract', 'temporary', 'internship'"
                },
                "remoteFilter": {
                    "type": "string",
                    "description": "Work arrangement: 'on site', 'remote', or 'hybrid'"
                },
                "experienceLevel": {
                    "type": "string",
                    "description": "Experience level: 'internship', 'entry level', 'associate', 'senior', 'director', 'executive'"
                },
                "limit": {
                    "type": "string",
                    "description": "Max number of results to return (default '10')"
                },
                "sortBy": {
                    "type": "string",
                    "description": "Sort order: 'recent' or 'relevant'"
                }
            },
            "required": ["keyword"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        try
        {
            var keyword = input["keyword"]!.GetValue<string>();
            var location = input["location"]?.GetValue<string>();
            var limit = int.TryParse(input["limit"]?.GetValue<string>(), out var l) ? l : 10;
            var dateSincePosted = input["dateSincePosted"]?.GetValue<string>();
            var sortBy = input["sortBy"]?.GetValue<string>();

            var dateFilter = MapDateFilter(dateSincePosted);
            var sortParam = sortBy == "recent" ? "&sortBy=DD" : "";

            var encodedKeyword = HttpUtility.UrlEncode(keyword);
            var encodedLocation = location is not null ? HttpUtility.UrlEncode(location) : "";

            var url = $"https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search?" +
                      $"keywords={encodedKeyword}" +
                      (string.IsNullOrEmpty(encodedLocation) ? "" : $"&location={encodedLocation}") +
                      (string.IsNullOrEmpty(dateFilter) ? "" : $"&f_TPR={dateFilter}") +
                      $"&start=0&count={limit}{sortParam}";

            var response = await Http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return $"Error fetching LinkedIn jobs: HTTP {(int)response.StatusCode}";

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var cards = doc.DocumentNode.SelectNodes("//li");
            if (cards is null || cards.Count == 0)
                return "No job postings found matching your criteria.";

            var results = new List<string>();
            var index = 0;
            foreach (var card in cards)
            {
                if (index >= limit) break;

                var title = card.SelectSingleNode(".//h3[contains(@class,'base-search-card__title')]")?.InnerText.Trim();
                var company = card.SelectSingleNode(".//h4[contains(@class,'base-search-card__subtitle')]")?.InnerText.Trim();
                var loc = card.SelectSingleNode(".//span[contains(@class,'job-search-card__location')]")?.InnerText.Trim();
                var posted = card.SelectSingleNode(".//time")?.InnerText.Trim();
                var salary = card.SelectSingleNode(".//span[contains(@class,'job-search-card__salary')]")?.InnerText.Trim() ?? "Not listed";
                var jobUrl = card.SelectSingleNode(".//a[contains(@class,'base-card__full-link')]")?.GetAttributeValue("href", "");

                if (title is null) continue;

                index++;
                results.Add(
                    $"{index}. {HttpUtility.HtmlDecode(title)}\n" +
                    $"   Company: {HttpUtility.HtmlDecode(company)}\n" +
                    $"   Location: {HttpUtility.HtmlDecode(loc)}\n" +
                    $"   Posted: {posted}\n" +
                    $"   Salary: {salary}\n" +
                    $"   URL: {jobUrl}");
            }

            return results.Count > 0
                ? string.Join("\n\n", results)
                : "No job postings found matching your criteria.";
        }
        catch (Exception ex)
        {
            return $"Error searching LinkedIn jobs: {ex.Message}";
        }
    }

    private static string? MapDateFilter(string? dateSincePosted) => dateSincePosted switch
    {
        "24hr" => "r86400",
        "past week" => "r604800",
        "past month" => "r2592000",
        _ => null,
    };
}
