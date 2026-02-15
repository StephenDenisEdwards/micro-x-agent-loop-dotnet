using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Gmail;

public class GmailSearchTool : ITool
{
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;

    public GmailSearchTool(string googleClientId, string googleClientSecret)
    {
        _googleClientId = googleClientId;
        _googleClientSecret = googleClientSecret;
    }

    public string Name => "gmail_search";

    public string Description =>
        "Search Gmail using Gmail search syntax (e.g. 'is:unread', 'from:someone@example.com', 'subject:hello'). Returns a list of matching emails with ID, date, from, subject, and snippet.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Gmail search query (e.g. 'is:unread', 'from:boss@co.com newer_than:7d')"
                },
                "maxResults": {
                    "type": "number",
                    "description": "Max number of results (default 10)"
                }
            },
            "required": ["query"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        try
        {
            var gmail = await GmailAuth.GetGmailServiceAsync(_googleClientId, _googleClientSecret);
            var query = input["query"]!.GetValue<string>();
            var maxResults = input["maxResults"]?.GetValue<int>() ?? 10;

            var listRequest = gmail.Users.Messages.List("me");
            listRequest.Q = query;
            listRequest.MaxResults = maxResults;

            var listResponse = await listRequest.ExecuteAsync();
            var messages = listResponse.Messages;

            if (messages is null || messages.Count == 0)
                return "No emails found matching your query.";

            var results = new List<string>();
            foreach (var msg in messages)
            {
                var detailRequest = gmail.Users.Messages.Get("me", msg.Id);
                detailRequest.Format = Google.Apis.Gmail.v1.UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
                detailRequest.MetadataHeaders = new[] { "From", "Subject", "Date" };

                var detail = await detailRequest.ExecuteAsync();

                var headers = detail.Payload?.Headers;
                var from = GmailParser.GetHeader(headers, "From");
                var subject = GmailParser.GetHeader(headers, "Subject");
                var date = GmailParser.GetHeader(headers, "Date");
                var snippet = detail.Snippet ?? "";

                results.Add(
                    $"ID: {msg.Id}\n" +
                    $"  Date: {date}\n" +
                    $"  From: {from}\n" +
                    $"  Subject: {subject}\n" +
                    $"  Snippet: {snippet}");
            }

            return string.Join("\n\n", results);
        }
        catch (Exception ex)
        {
            return $"Error searching Gmail: {ex.Message}";
        }
    }
}
