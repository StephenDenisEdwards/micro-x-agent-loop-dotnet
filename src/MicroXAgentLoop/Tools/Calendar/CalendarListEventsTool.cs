using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Calendar;

public class CalendarListEventsTool : ITool
{
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;

    public CalendarListEventsTool(string googleClientId, string googleClientSecret)
    {
        _googleClientId = googleClientId;
        _googleClientSecret = googleClientSecret;
    }

    public string Name => "calendar_list_events";

    public string Description =>
        "List Google Calendar events by date range or search query. " +
        "Returns event ID, summary, start/end times, location, status, and organizer. " +
        "Defaults to today's events if no time range is specified.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "timeMin": {
                    "type": "string",
                    "description": "Start of time range in ISO 8601 format (e.g. '2025-06-01T00:00:00Z'). Defaults to start of today."
                },
                "timeMax": {
                    "type": "string",
                    "description": "End of time range in ISO 8601 format (e.g. '2025-06-01T23:59:59Z'). Defaults to end of today."
                },
                "query": {
                    "type": "string",
                    "description": "Free-text search query to filter events (searches summary, description, location, attendees)."
                },
                "maxResults": {
                    "type": "number",
                    "description": "Max number of results (default 10)."
                },
                "calendarId": {
                    "type": "string",
                    "description": "Calendar ID (default 'primary')."
                }
            },
            "required": []
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        try
        {
            var cal = await CalendarAuth.GetCalendarServiceAsync(_googleClientId, _googleClientSecret);

            var timeMin = input["timeMin"]?.GetValue<string>();
            var timeMax = input["timeMax"]?.GetValue<string>();
            var query = input["query"]?.GetValue<string>();
            var maxResults = input["maxResults"]?.GetValue<int>() ?? 10;
            var calendarId = input["calendarId"]?.GetValue<string>() ?? "primary";

            if (string.IsNullOrEmpty(timeMin) && string.IsNullOrEmpty(timeMax))
            {
                var now = DateTime.UtcNow;
                timeMin = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc)
                    .ToString("o");
                timeMax = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59, DateTimeKind.Utc)
                    .ToString("o");
            }

            var request = cal.Events.List(calendarId);
            request.MaxResults = maxResults;
            request.SingleEvents = true;
            request.OrderBy = Google.Apis.Calendar.v3.EventsResource.ListRequest.OrderByEnum.StartTime;

            if (!string.IsNullOrEmpty(timeMin))
                request.TimeMinDateTimeOffset = DateTimeOffset.Parse(timeMin);
            if (!string.IsNullOrEmpty(timeMax))
                request.TimeMaxDateTimeOffset = DateTimeOffset.Parse(timeMax);
            if (!string.IsNullOrEmpty(query))
                request.Q = query;

            var response = await request.ExecuteAsync();
            var events = response.Items;

            if (events is null || events.Count == 0)
                return "No events found.";

            var results = new List<string>();
            foreach (var ev in events)
            {
                var start = ev.Start?.DateTimeDateTimeOffset?.ToString("o")
                    ?? ev.Start?.Date ?? "";
                var end = ev.End?.DateTimeDateTimeOffset?.ToString("o")
                    ?? ev.End?.Date ?? "";
                var organizer = ev.Organizer?.Email ?? "";

                results.Add(
                    $"ID: {ev.Id}\n" +
                    $"  Summary: {ev.Summary ?? "(no title)"}\n" +
                    $"  Start: {start}\n" +
                    $"  End: {end}\n" +
                    $"  Location: {ev.Location ?? ""}\n" +
                    $"  Status: {ev.Status ?? ""}\n" +
                    $"  Organizer: {organizer}");
            }

            return string.Join("\n\n", results);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  calendar_list_events error: {ex.Message}");
            return $"Error listing calendar events: {ex.Message}";
        }
    }
}
