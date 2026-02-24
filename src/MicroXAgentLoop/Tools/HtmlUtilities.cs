using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace MicroXAgentLoop.Tools;

public static class HtmlUtilities
{
    private static readonly Regex TabsRegex = new(@"\t+", RegexOptions.Compiled);
    private static readonly Regex ExcessSpacesRegex = new(@" {3,}", RegexOptions.Compiled);
    private static readonly Regex ExcessNewlinesRegex = new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>
    /// Convert an HTML string to readable plain text.
    /// Handles block elements, lists, table cells, and whitespace normalization.
    /// </summary>
    public static string HtmlToText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove non-visible elements
        var removeNodes = doc.DocumentNode.SelectNodes("//script|//style|//head");
        if (removeNodes is not null)
            foreach (var node in removeNodes)
                node.Remove();

        // Preserve links as "text (url)" before stripping tags
        var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (linkNodes is not null)
            foreach (var a in linkNodes)
            {
                var href = a.GetAttributeValue("href", "");
                var linkText = a.InnerText.Trim();
                var replacement = string.IsNullOrEmpty(linkText) ? href : $"{linkText} ({href})";
                a.ParentNode.ReplaceChild(HtmlNode.CreateNode(HttpUtility.HtmlEncode(replacement)), a);
            }

        // Replace <br> with newlines
        var brNodes = doc.DocumentNode.SelectNodes("//br");
        if (brNodes is not null)
            foreach (var br in brNodes)
                br.ParentNode.ReplaceChild(HtmlNode.CreateNode("\n"), br);

        // Add newlines around block elements
        var blockNodes = doc.DocumentNode.SelectNodes(
            "//p|//div|//tr|//h1|//h2|//h3|//h4|//h5|//h6|//blockquote");
        if (blockNodes is not null)
            foreach (var el in blockNodes)
                el.InnerHtml = "\n" + el.InnerHtml + "\n";

        // Bullet list items
        var liNodes = doc.DocumentNode.SelectNodes("//li");
        if (liNodes is not null)
            foreach (var li in liNodes)
                li.InnerHtml = "\n- " + li.InnerHtml;

        // Table cells
        var tdNodes = doc.DocumentNode.SelectNodes("//td|//th");
        if (tdNodes is not null)
            foreach (var td in tdNodes)
                td.InnerHtml += "\t";

        var text = HttpUtility.HtmlDecode(
            doc.DocumentNode.SelectSingleNode("//body")?.InnerText
            ?? doc.DocumentNode.InnerText);

        text = TabsRegex.Replace(text, "  ");
        text = ExcessSpacesRegex.Replace(text, "  ");
        text = ExcessNewlinesRegex.Replace(text, "\n\n");
        return text.Trim();
    }
}
