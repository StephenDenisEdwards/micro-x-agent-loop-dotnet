# Tool: contacts_list

List Google Contacts with pagination and sort options.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `pageSize` | number | No | Number of contacts to return (default 10, max 100) |
| `pageToken` | string | No | Page token from a previous response for pagination |
| `sortOrder` | string | No | Sort order (see below) |

### Sort Order Values

| Value | Description |
|-------|-------------|
| `LAST_MODIFIED_ASCENDING` | Oldest modified first |
| `LAST_MODIFIED_DESCENDING` | Most recently modified first |
| `FIRST_NAME_ASCENDING` | Alphabetical by first name |
| `LAST_NAME_ASCENDING` | Alphabetical by last name |

## Behavior

- Uses the Google People API `people.connections.list` endpoint
- Returns contacts with name, email, and phone number (summary format)
- Includes a `nextPageToken` when more results are available
- Resource names in results can be used with `contacts_get` for full details
- **Conditional registration:** Only available when `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `.env`

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Contacts/ContactsListTool.cs`
- Uses `Google.Apis.PeopleService.v1` NuGet package
- PersonFields: `names,emailAddresses,phoneNumbers`
- OAuth2 via `ContactsAuth.GetContactsServiceAsync()`

## Example

```
you> List my first 20 contacts sorted by last name
```

Claude calls:
```json
{
  "name": "contacts_list",
  "input": {
    "pageSize": 20,
    "sortOrder": "LAST_NAME_ASCENDING"
  }
}
```

## Authentication

On first use, a browser window opens for Google OAuth sign-in. Tokens are cached in `.contacts-tokens/` for future sessions. See [Getting Started](../../../operations/getting-started.md) for setup instructions.
