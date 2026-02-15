using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace MicroXAgentLoop.Tools.LinkedIn;

public class LinkedInJobDetailTool : ITool
{
    private static readonly HttpClient Http = new();

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
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.5");

            var response = await Http.SendAsync(request);
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
                description = HtmlToPlainText(descNode);
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

    private static string HtmlToPlainText(HtmlNode node)
    {
        var html = node.InnerHtml;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Replace <br> with newlines
        var brNodes = doc.DocumentNode.SelectNodes("//br");
        if (brNodes is not null)
            foreach (var br in brNodes)
                br.ParentNode.ReplaceChild(HtmlNode.CreateNode("\n"), br);

        // Add newlines around block elements
        var blockNodes = doc.DocumentNode.SelectNodes("//p|//div|//h1|//h2|//h3|//h4|//h5|//h6");
        if (blockNodes is not null)
            foreach (var el in blockNodes)
                el.InnerHtml = "\n" + el.InnerHtml + "\n";

        // Add bullet for list items
        var liNodes = doc.DocumentNode.SelectNodes("//li");
        if (liNodes is not null)
            foreach (var li in liNodes)
                li.InnerHtml = "- " + li.InnerHtml + "\n";

        var text = HttpUtility.HtmlDecode(doc.DocumentNode.InnerText);
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}
