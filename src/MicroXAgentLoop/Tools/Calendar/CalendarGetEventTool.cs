using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Calendar;

public class CalendarGetEventTool : ITool
{
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;

    public CalendarGetEventTool(string googleClientId, string googleClientSecret)
    {
        _googleClientId = googleClientId;
        _googleClientSecret = googleClientSecret;
    }

    public string Name => "calendar_get_event";

    public string Description => "Get full details of a Google Calendar event by its event ID.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "eventId": {
                    "type": "string",
                    "description": "The event ID (from calendar_list_events results)."
                },
                "calendarId": {
                    "type": "string",
                    "description": "Calendar ID (default 'primary')."
                }
            },
            "required": ["eventId"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        try
        {
            var cal = await CalendarAuth.GetCalendarServiceAsync(_googleClientId, _googleClientSecret);
            var eventId = input["eventId"]!.GetValue<string>();
            var calendarId = input["calendarId"]?.GetValue<string>() ?? "primary";

            var ev = await cal.Events.Get(calendarId, eventId).ExecuteAsync();

            var start = ev.Start?.DateTimeDateTimeOffset?.ToString("o")
                ?? ev.Start?.Date ?? "";
            var end = ev.End?.DateTimeDateTimeOffset?.ToString("o")
                ?? ev.End?.Date ?? "";

            var lines = new List<string>
            {
                $"Summary: {ev.Summary ?? "(no title)"}",
                $"Status: {ev.Status ?? ""}",
                $"Start: {start}",
                $"End: {end}",
                $"Location: {ev.Location ?? ""}",
                $"Description: {ev.Description ?? ""}",
                $"Organizer: {ev.Organizer?.Email ?? ""}",
                $"Creator: {ev.Creator?.Email ?? ""}",
            };

            if (ev.Attendees is { Count: > 0 })
            {
                var attendeeLines = ev.Attendees
                    .Select(a => $"    {a.Email} ({a.ResponseStatus})")
                    .ToList();
                lines.Add("Attendees:\n" + string.Join("\n", attendeeLines));
            }

            if (ev.ConferenceData?.EntryPoints is not null)
            {
                var videoEntry = ev.ConferenceData.EntryPoints
                    .FirstOrDefault(ep => ep.EntryPointType == "video");
                if (videoEntry is not null)
                    lines.Add($"Conference Link: {videoEntry.Uri}");
            }

            if (ev.Recurrence is { Count: > 0 })
                lines.Add($"Recurrence: {string.Join("; ", ev.Recurrence)}");

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  calendar_get_event error: {ex.Message}");
            return $"Error getting calendar event: {ex.Message}";
        }
    }
}
