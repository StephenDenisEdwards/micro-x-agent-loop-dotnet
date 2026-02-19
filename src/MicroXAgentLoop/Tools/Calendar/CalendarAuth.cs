using Google.Apis.Calendar.v3;
using Google.Apis.Http;
using Google.Apis.Services;

namespace MicroXAgentLoop.Tools.Calendar;

public sealed class CalendarAuth : GoogleAuthBase<CalendarService>
{
    public static readonly CalendarAuth Instance = new();

    protected override string[] Scopes =>
    [
        CalendarService.Scope.Calendar,
    ];

    protected override string TokenDirectory => ".calendar-tokens";

    protected override CalendarService CreateService(IConfigurableHttpClientInitializer credential) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "micro-x-agent-loop",
        });
}
