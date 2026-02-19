using Google.Apis.Gmail.v1;
using Google.Apis.Http;
using Google.Apis.Services;

namespace MicroXAgentLoop.Tools.Gmail;

public sealed class GmailAuth : GoogleAuthBase<GmailService>
{
    public static readonly GmailAuth Instance = new();

    protected override string[] Scopes =>
    [
        GmailService.Scope.GmailReadonly,
        GmailService.Scope.GmailSend,
    ];

    protected override string TokenDirectory => ".gmail-tokens";

    protected override GmailService CreateService(IConfigurableHttpClientInitializer credential) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "micro-x-agent-loop",
        });
}
