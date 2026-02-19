using Google.Apis.Http;
using Google.Apis.PeopleService.v1;
using Google.Apis.Services;

namespace MicroXAgentLoop.Tools.Contacts;

public sealed class ContactsAuth : GoogleAuthBase<PeopleServiceService>
{
    public static readonly ContactsAuth Instance = new();

    protected override string[] Scopes =>
    [
        PeopleServiceService.Scope.Contacts,
    ];

    protected override string TokenDirectory => ".contacts-tokens";

    protected override PeopleServiceService CreateService(IConfigurableHttpClientInitializer credential) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "micro-x-agent-loop",
        });
}
