using System.Text.Json.Nodes;
using System.Web;
using HtmlAgilityPack;
using MicroXAgentLoop.Tools;

namespace MicroXAgentLoop.Tools.LinkedIn;

public class LinkedInJobDetailTool : ITool
{
    private static HttpClient Http => HttpClientFactory.Browser;

    public string Name => "linkedin_job_detail";

    public string Description =>
        "Fetch the full job specification/description from a LinkedIn job URL. Use this after linkedin_jobs to get complete details for a specific posting.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "The LinkedIn job URL (e.g. from a linkedin_jobs search result)"
                }
            },
            "required": ["url"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        var url = input["url"]!.GetValue<string>();
        try
        {
            var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return $"Error fetching job page: HTTP {(int)response.StatusCode}";

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title =
                doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'top-card-layout__title')]")?.InnerText.Trim()
                ?? doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.Trim();

            var company =
                doc.DocumentNode.SelectSingleNode("//a[contains(@class,'topcard__org-name-link')]")?.InnerText.Trim()
                ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class,'top-card-layout__company-name')]")?.InnerText.Trim();

            var location =
                doc.DocumentNode.SelectSingleNode("//span[contains(@class,'topcard__flavor--bullet')]")?.InnerText.Trim()
                ?? doc.DocumentNode.SelectSingleNode("//span[contains(@class,'top-card-layout__bullet')]")?.InnerText.Trim();

            var descNode =
                doc.DocumentNode.SelectSingleNode("//*[contains(@class,'description__text')]")
                ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class,'show-more-less-html__markup')]")
                ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class,'decorated-job-posting__details')]");

            var description = string.Empty;
            if (descNode is not null)
            {
                description = HtmlUtilities.HtmlToText(descNode.InnerHtml);
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return "Could not extract job description from the page. LinkedIn may have blocked the request or the page structure has changed.";
            }

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(title)) parts.Add($"Title: {HttpUtility.HtmlDecode(title)}");
            if (!string.IsNullOrEmpty(company)) parts.Add($"Company: {HttpUtility.HtmlDecode(company)}");
            if (!string.IsNullOrEmpty(location)) parts.Add($"Location: {HttpUtility.HtmlDecode(location)}");
            parts.Add("");
            parts.Add("--- Job Description ---");
            parts.Add("");
            parts.Add(description);

            return string.Join("\n", parts);
        }
        catch (Exception ex)
        {
            return $"Error fetching job details: {ex.Message}";
        }
    }

}
