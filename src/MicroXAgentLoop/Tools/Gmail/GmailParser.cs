using System.Text;
using Google.Apis.Gmail.v1.Data;

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
                return HtmlUtilities.HtmlToText(DecodeBody(payload.Body.Data));
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
                    return HtmlUtilities.HtmlToText(DecodeBody(part.Body.Data));
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
