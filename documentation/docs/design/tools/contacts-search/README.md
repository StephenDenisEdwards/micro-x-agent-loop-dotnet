# Tool: contacts_search

Search Google Contacts by name, email, phone number, or other fields.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | Search query (name, email, phone number, etc.) |
| `pageSize` | number | No | Max number of results (default 10, max 30) |

## Behavior

- Uses the Google People API `searchContacts` endpoint
- Returns matching contacts with name, email, and phone number (summary format)
- Resource names in results can be used with `contacts_get` for full details
- **Conditional registration:** Only available when `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `.env`

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Contacts/ContactsSearchTool.cs`
- Uses `Google.Apis.PeopleService.v1` NuGet package
- ReadMask: `names,emailAddresses,phoneNumbers`
- OAuth2 via `ContactsAuth.Instance.GetServiceAsync()` (extends `GoogleAuthBase<PeopleServiceService>`)

## Example

```
you> Search my contacts for John Smith
```

Claude calls:
```json
{
  "name": "contacts_search",
  "input": {
    "query": "John Smith",
    "pageSize": 10
  }
}
```

## Authentication

On first use, a browser window opens for Google OAuth sign-in. Tokens are cached in `.contacts-tokens/` for future sessions. See [Getting Started](../../../operations/getting-started.md) for setup instructions.
