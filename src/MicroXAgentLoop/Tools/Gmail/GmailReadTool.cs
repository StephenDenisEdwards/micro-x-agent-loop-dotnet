using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Gmail;

public class GmailReadTool : GoogleToolBase
{
    public GmailReadTool(string googleClientId, string googleClientSecret)
        : base(googleClientId, googleClientSecret) { }

    public override string Name => "gmail_read";

    public override string Description =>
        "Read the full content of a Gmail email by its message ID (from gmail_search results).";

    private static readonly JsonNode Schema = JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "messageId": {
                    "type": "string",
                    "description": "The Gmail message ID (from gmail_search results)"
                }
            },
            "required": ["messageId"]
        }
        """)!;

    public override JsonNode InputSchema => Schema;

    public override async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        try
        {
            var gmail = await GmailAuth.Instance.GetServiceAsync(GoogleClientId, GoogleClientSecret);
            var messageId = input["messageId"]!.GetValue<string>();

            var request = gmail.Users.Messages.Get("me", messageId);
            request.Format = Google.Apis.Gmail.v1.UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

            var message = await request.ExecuteAsync(ct);
            var headers = message.Payload?.Headers;
            var from = GmailParser.GetHeader(headers, "From");
            var to = GmailParser.GetHeader(headers, "To");
            var subject = GmailParser.GetHeader(headers, "Subject");
            var date = GmailParser.GetHeader(headers, "Date");

            var body = message.Payload is not null
                ? GmailParser.ExtractText(message.Payload)
                : "(no text content)";

            if (string.IsNullOrEmpty(body))
                body = "(no text content)";

            return $"From: {from}\nTo: {to}\nDate: {date}\nSubject: {subject}\n\n{body}";
        }
        catch (Exception ex)
        {
            return HandleError(ex.Message);
        }
    }
}
