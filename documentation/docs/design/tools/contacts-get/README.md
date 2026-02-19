# Tool: contacts_get

Get full details of a Google Contact by resource name.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `resourceName` | string | Yes | The contact's resource name (e.g. `people/c1234567890`) |

## Behavior

- Uses the Google People API `people.get` endpoint
- Returns full contact details: name, emails, phones, addresses, organization, biography, and etag
- The etag is required for `contacts_update` (concurrency control)
- **Conditional registration:** Only available when `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `.env`

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Contacts/ContactsGetTool.cs`
- Uses `Google.Apis.PeopleService.v1` NuGet package
- PersonFields: `names,emailAddresses,phoneNumbers,addresses,organizations,biographies`
- Uses `ContactsFormatter.FormatContactDetail()` for full output
- OAuth2 via `ContactsAuth.Instance.GetServiceAsync()` (extends `GoogleAuthBase<PeopleServiceService>`)

## Example

```
you> Get the full details for contact people/c1234567890
```

Claude calls:
```json
{
  "name": "contacts_get",
  "input": {
    "resourceName": "people/c1234567890"
  }
}
```

## Authentication

On first use, a browser window opens for Google OAuth sign-in. Tokens are cached in `.contacts-tokens/` for future sessions. See [Getting Started](../../../operations/getting-started.md) for setup instructions.
