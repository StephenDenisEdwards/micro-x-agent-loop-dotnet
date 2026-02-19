using Google.Apis.Auth.OAuth2;
using Google.Apis.PeopleService.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace MicroXAgentLoop.Tools.Contacts;

public static class ContactsAuth
{
    private static readonly string[] Scopes =
    [
        PeopleServiceService.Scope.Contacts,
    ];

    private static PeopleServiceService? _service;

    public static async Task<PeopleServiceService> GetContactsServiceAsync(string clientId, string clientSecret)
    {
        if (_service is not null)
            return _service;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException(
                "GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET must be set in .env");

        var secrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
        };

        var tokenPath = Path.Combine(Directory.GetCurrentDirectory(), ".contacts-tokens");

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(tokenPath, true));

        _service = new PeopleServiceService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "micro-x-agent-loop",
        });

        return _service;
    }
}
