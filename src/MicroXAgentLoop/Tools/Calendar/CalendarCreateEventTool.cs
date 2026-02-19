using System.Text.Json.Nodes;
using Google.Apis.Calendar.v3.Data;

namespace MicroXAgentLoop.Tools.Calendar;

public class CalendarCreateEventTool : GoogleToolBase
{
    public CalendarCreateEventTool(string googleClientId, string googleClientSecret)
        : base(googleClientId, googleClientSecret) { }

    public override string Name => "calendar_create_event";

    public override string Description =>
        "Create a Google Calendar event. Supports timed events (ISO 8601 with time) " +
        "and all-day events (YYYY-MM-DD date only). Can add attendees by email.";

    public override JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "summary": {
                    "type": "string",
                    "description": "Event title."
                },
                "start": {
                    "type": "string",
                    "description": "Start time in ISO 8601 (e.g. '2025-06-15T14:00:00') or date only for all-day events (e.g. '2025-06-15')."
                },
                "end": {
                    "type": "string",
                    "description": "End time in ISO 8601 (e.g. '2025-06-15T15:00:00') or date only for all-day events (e.g. '2025-06-16')."
                },
                "description": {
                    "type": "string",
                    "description": "Event description/notes."
                },
                "location": {
                    "type": "string",
                    "description": "Event location."
                },
                "attendees": {
                    "type": "string",
                    "description": "Comma-separated email addresses of attendees."
                },
                "calendarId": {
                    "type": "string",
                    "description": "Calendar ID (default 'primary')."
                }
            },
            "required": ["summary", "start", "end"]
        }
        """)!;

    public override async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        try
        {
            var cal = await CalendarAuth.Instance.GetServiceAsync(GoogleClientId, GoogleClientSecret);

            var summary = input["summary"]!.GetValue<string>();
            var start = input["start"]!.GetValue<string>();
            var end = input["end"]!.GetValue<string>();
            var description = input["description"]?.GetValue<string>() ?? "";
            var location = input["location"]?.GetValue<string>() ?? "";
            var attendeesStr = input["attendees"]?.GetValue<string>() ?? "";
            var calendarId = input["calendarId"]?.GetValue<string>() ?? "primary";

            var isAllDay = !start.Contains('T');

            var eventBody = new Event
            {
                Summary = summary,
                Start = isAllDay
                    ? new EventDateTime { Date = start }
                    : new EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.Parse(start) },
                End = isAllDay
                    ? new EventDateTime { Date = end }
                    : new EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.Parse(end) },
            };

            if (!string.IsNullOrEmpty(description))
                eventBody.Description = description;
            if (!string.IsNullOrEmpty(location))
                eventBody.Location = location;
            if (!string.IsNullOrEmpty(attendeesStr))
            {
                var emails = attendeesStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                eventBody.Attendees = emails.Select(e => new EventAttendee { Email = e }).ToList();
            }

            var created = await cal.Events.Insert(eventBody, calendarId).ExecuteAsync();

            var startDisplay = created.Start?.DateTimeDateTimeOffset?.ToString("o")
                ?? created.Start?.Date ?? "";
            var endDisplay = created.End?.DateTimeDateTimeOffset?.ToString("o")
                ?? created.End?.Date ?? "";

            return
                $"Event created successfully.\n" +
                $"  ID: {created.Id}\n" +
                $"  Summary: {created.Summary}\n" +
                $"  Start: {startDisplay}\n" +
                $"  End: {endDisplay}\n" +
                $"  Link: {created.HtmlLink}";
        }
        catch (Exception ex)
        {
            return HandleError(ex.Message);
        }
    }
}
