using System.Text;
using System.Text.Json.Nodes;
using Google.Apis.Gmail.v1.Data;

namespace MicroXAgentLoop.Tools.Gmail;

public class GmailSendTool : ITool
{
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;

    public GmailSendTool(string googleClientId, string googleClientSecret)
    {
        _googleClientId = googleClientId;
        _googleClientSecret = googleClientSecret;
    }

    public string Name => "gmail_send";
    public string Description => "Send an email from your Gmail account.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "to": {
                    "type": "string",
                    "description": "Recipient email address"
                },
                "subject": {
                    "type": "string",
                    "description": "Email subject line"
                },
                "body": {
                    "type": "string",
                    "description": "Email body (plain text)"
                }
            },
            "required": ["to", "subject", "body"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        try
        {
            var gmail = await GmailAuth.GetGmailServiceAsync(_googleClientId, _googleClientSecret);
            var to = input["to"]!.GetValue<string>();
            var subject = input["subject"]!.GetValue<string>();
            var body = input["body"]!.GetValue<string>();

            var message = string.Join("\r\n",
                $"To: {to}",
                $"Subject: {subject}",
                "Content-Type: text/plain; charset=utf-8",
                "",
                body);

            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(message))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            var result = await gmail.Users.Messages.Send(
                new Message { Raw = raw }, "me").ExecuteAsync();

            return $"Email sent successfully (ID: {result.Id})";
        }
        catch (Exception ex)
        {
            return $"Error sending email: {ex.Message}";
        }
    }
}
