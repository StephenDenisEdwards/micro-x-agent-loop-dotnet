using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace MicroXAgentLoop.Tools.Gmail;

public static class GmailAuth
{
    private static readonly string[] Scopes =
    [
        GmailService.Scope.GmailReadonly,
        GmailService.Scope.GmailSend,
    ];

    private static GmailService? _service;

    public static async Task<GmailService> GetGmailServiceAsync()
    {
        if (_service is not null)
            return _service;

        var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException(
                "GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET must be set in .env");

        var secrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
        };

        var tokenPath = Path.Combine(Directory.GetCurrentDirectory(), ".gmail-tokens");

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(tokenPath, true));

        _service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "micro-x-agent-loop",
        });

        return _service;
    }
}
