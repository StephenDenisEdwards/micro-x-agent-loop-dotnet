# Tool: calendar_list_events

List Google Calendar events by date range or search query.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `timeMin` | string | No | Start of time range in ISO 8601 format (e.g. `"2025-06-01T00:00:00Z"`). Defaults to start of today. |
| `timeMax` | string | No | End of time range in ISO 8601 format (e.g. `"2025-06-01T23:59:59Z"`). Defaults to end of today. |
| `query` | string | No | Free-text search query to filter events (searches summary, description, location, attendees). |
| `maxResults` | number | No | Max number of results (default 10). |
| `calendarId` | string | No | Calendar ID (default `"primary"`). |

## Behavior

- Uses the Google Calendar API `events.list` endpoint
- Defaults to today's events if neither `timeMin` nor `timeMax` is provided
- Returns a formatted list with: event ID, summary, start/end times, location, status, and organizer
- Recurring events are expanded into individual instances (`SingleEvents=true`)
- Results are sorted by start time
- The event ID can be used with `calendar_get_event` to fetch full details
- **Conditional registration:** Only available when `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `.env`

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Calendar/CalendarListEventsTool.cs`
- Uses `Google.Apis.Calendar.v3` NuGet package for Calendar API access
- OAuth2 via `CalendarAuth.GetCalendarServiceAsync()`

## Example

```
you> What meetings do I have today?
```

Claude calls:
```json
{
  "name": "calendar_list_events",
  "input": {}
}
```

## Authentication

On first use, a browser window opens for Google OAuth sign-in. Tokens are cached in `.calendar-tokens/` for future sessions. See [Getting Started](../../../operations/getting-started.md) for setup instructions.
