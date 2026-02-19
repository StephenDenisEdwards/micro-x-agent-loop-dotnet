# Tool: contacts_delete

Delete a Google Contact by resource name. This action cannot be undone.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `resourceName` | string | Yes | The contact's resource name (e.g. `people/c1234567890`) |

## Behavior

- Uses the Google People API `people.deleteContact` endpoint
- Permanently deletes the contact — this cannot be undone
- Returns a confirmation message on success
- **Conditional registration:** Only available when `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `.env`

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Contacts/ContactsDeleteTool.cs`
- Uses `Google.Apis.PeopleService.v1` NuGet package
- OAuth2 via `ContactsAuth.GetContactsServiceAsync()`

## Example

```
you> Delete contact people/c1234567890
```

Claude calls:
```json
{
  "name": "contacts_delete",
  "input": {
    "resourceName": "people/c1234567890"
  }
}
```

## Authentication

On first use, a browser window opens for Google OAuth sign-in. Tokens are cached in `.contacts-tokens/` for future sessions. See [Getting Started](../../../operations/getting-started.md) for setup instructions.
