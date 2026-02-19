using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Calendar;

public class CalendarGetEventTool : GoogleToolBase
{
    public CalendarGetEventTool(string googleClientId, string googleClientSecret)
        : base(googleClientId, googleClientSecret) { }

    public override string Name => "calendar_get_event";

    public override string Description => "Get full details of a Google Calendar event by its event ID.";

    public override JsonNode InputSchema => JsonNode.Parse("""
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

    public override async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        try
        {
            var cal = await CalendarAuth.Instance.GetServiceAsync(GoogleClientId, GoogleClientSecret);
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
            return HandleError(ex.Message);
        }
    }
}
