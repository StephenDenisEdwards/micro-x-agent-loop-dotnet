using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Gmail;

public class GmailSearchTool : GoogleToolBase
{
    public GmailSearchTool(string googleClientId, string googleClientSecret)
        : base(googleClientId, googleClientSecret) { }

    public override string Name => "gmail_search";

    public override string Description =>
        "Search Gmail using Gmail search syntax (e.g. 'is:unread', 'from:someone@example.com', 'subject:hello'). Returns a list of matching emails with ID, date, from, subject, and snippet.";

    private static readonly JsonNode Schema = JsonNode.Parse("""
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

    public override JsonNode InputSchema => Schema;

    public override async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        try
        {
            var gmail = await GmailAuth.Instance.GetServiceAsync(GoogleClientId, GoogleClientSecret);
            var query = input["query"]!.GetValue<string>();
            var maxResults = input["maxResults"]?.GetValue<int>() ?? 10;

            var listRequest = gmail.Users.Messages.List("me");
            listRequest.Q = query;
            listRequest.MaxResults = maxResults;

            var listResponse = await listRequest.ExecuteAsync(ct);
            var messages = listResponse.Messages;

            if (messages is null || messages.Count == 0)
                return "No emails found matching your query.";

            var tasks = messages.Select(async msg =>
            {
                var detailRequest = gmail.Users.Messages.Get("me", msg.Id);
                detailRequest.Format = Google.Apis.Gmail.v1.UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
                detailRequest.MetadataHeaders = new[] { "From", "Subject", "Date" };

                var detail = await detailRequest.ExecuteAsync(ct);

                var headers = detail.Payload?.Headers;
                var from = GmailParser.GetHeader(headers, "From");
                var subject = GmailParser.GetHeader(headers, "Subject");
                var date = GmailParser.GetHeader(headers, "Date");
                var snippet = detail.Snippet ?? "";

                return
                    $"ID: {msg.Id}\n" +
                    $"  Date: {date}\n" +
                    $"  From: {from}\n" +
                    $"  Subject: {subject}\n" +
                    $"  Snippet: {snippet}";
            });

            var results = await Task.WhenAll(tasks);

            return string.Join("\n\n", results);
        }
        catch (Exception ex)
        {
            return HandleError(ex.Message);
        }
    }
}
