# Tool: contacts_update

Update an existing Google Contact. Requires the resource name and etag for concurrency control.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `resourceName` | string | Yes | The contact's resource name (e.g. `people/c1234567890`) |
| `etag` | string | Yes | The contact's etag from `contacts_get` (concurrency control) |
| `givenName` | string | No | New first/given name |
| `familyName` | string | No | New last/family name |
| `email` | string | No | New email address (replaces existing emails) |
| `emailType` | string | No | Email type: `home`, `work`, or `other` (default `other`) |
| `phone` | string | No | New phone number (replaces existing phones) |
| `phoneType` | string | No | Phone type: `home`, `work`, `mobile`, or `other` (default `other`) |
| `organization` | string | No | New company/organization name |
| `jobTitle` | string | No | New job title |

## Behavior

- Uses the Google People API `people.updateContact` endpoint
- Only updates the fields you provide — unspecified fields are left unchanged
- Requires `etag` to prevent conflicting concurrent edits (get it from `contacts_get`)
- Returns the full updated contact details
- Returns an error if no updatable fields are provided
- **Conditional registration:** Only available when `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `.env`

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Contacts/ContactsUpdateTool.cs`
- Uses `Google.Apis.PeopleService.v1` NuGet package
- Builds `updatePersonFields` dynamically from provided fields (names, emailAddresses, phoneNumbers, organizations)
- OAuth2 via `ContactsAuth.GetContactsServiceAsync()`

## Example

```
you> Update contact people/c1234567890 to change their email to new@example.com
```

Claude first calls `contacts_get` to retrieve the etag, then:
```json
{
  "name": "contacts_update",
  "input": {
    "resourceName": "people/c1234567890",
    "etag": "%EgUBAi43...",
    "email": "new@example.com",
    "emailType": "work"
  }
}
```

## Authentication

On first use, a browser window opens for Google OAuth sign-in. Tokens are cached in `.contacts-tokens/` for future sessions. See [Getting Started](../../../operations/getting-started.md) for setup instructions.
