using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Google.Apis.Gmail.v1.Data;
using HtmlAgilityPack;

namespace MicroXAgentLoop.Tools.Gmail;

public static class GmailParser
{
    public static string GetHeader(IList<MessagePartHeader>? headers, string name)
    {
        if (headers is null) return string.Empty;
        return headers
            .FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Value ?? string.Empty;
    }

    public static string DecodeBody(string data)
    {
        // Gmail uses base64url encoding (- instead of +, _ instead of /)
        var base64 = data.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    public static string HtmlToText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove non-visible elements
        var removeNodes = doc.DocumentNode.SelectNodes("//script|//style|//head");
        if (removeNodes is not null)
            foreach (var node in removeNodes)
                node.Remove();

        // Replace <br> with newlines
        var brNodes = doc.DocumentNode.SelectNodes("//br");
        if (brNodes is not null)
            foreach (var br in brNodes)
                br.ParentNode.ReplaceChild(HtmlNode.CreateNode("\n"), br);

        // Add newlines around block elements
        var blockNodes = doc.DocumentNode.SelectNodes("//p|//div|//tr|//h1|//h2|//h3|//h4|//h5|//h6|//blockquote");
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

        text = Regex.Replace(text, @"\t+", "  ");
        text = Regex.Replace(text, @" {3,}", "  ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    /// <summary>
    /// Recursively extract the best text from a message payload.
    /// For multipart/alternative, prefer HTML (richest representation).
    /// For other multipart types, concatenate all readable sub-parts.
    /// </summary>
    public static string ExtractText(MessagePart payload)
    {
        // Leaf node with data
        if (!string.IsNullOrEmpty(payload.Body?.Data))
        {
            if (payload.MimeType == "text/plain")
                return DecodeBody(payload.Body.Data);
            if (payload.MimeType == "text/html")
                return HtmlToText(DecodeBody(payload.Body.Data));
        }

        if (payload.Parts is null || payload.Parts.Count == 0)
            return string.Empty;

        // multipart/alternative — pick the richest version
        if (payload.MimeType == "multipart/alternative")
        {
            // Try HTML first (usually last part, most complete)
            foreach (var part in payload.Parts.Reverse())
            {
                if (part.MimeType == "text/html" && !string.IsNullOrEmpty(part.Body?.Data))
                    return HtmlToText(DecodeBody(part.Body.Data));
            }
            // Recurse into nested multipart children
            foreach (var part in payload.Parts.Reverse())
            {
                if (part.MimeType?.StartsWith("multipart/") == true)
                {
                    var text = ExtractText(part);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            // Fall back to text/plain
            foreach (var part in payload.Parts)
            {
                if (part.MimeType == "text/plain" && !string.IsNullOrEmpty(part.Body?.Data))
                    return DecodeBody(part.Body.Data);
            }
        }

        // multipart/mixed, multipart/related, etc. — concatenate all readable parts
        var sections = new List<string>();
        foreach (var part in payload.Parts)
        {
            var text2 = ExtractText(part);
            if (!string.IsNullOrEmpty(text2)) sections.Add(text2);
        }
        return string.Join("\n\n", sections);
    }
}
