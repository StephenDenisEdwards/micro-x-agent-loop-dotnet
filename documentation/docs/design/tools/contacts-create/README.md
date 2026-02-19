# Tool: contacts_create

Create a new Google Contact.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `givenName` | string | Yes | First/given name |
| `familyName` | string | No | Last/family name |
| `email` | string | No | Email address |
| `emailType` | string | No | Email type: `home`, `work`, or `other` (default `other`) |
| `phone` | string | No | Phone number |
| `phoneType` | string | No | Phone type: `home`, `work`, `mobile`, or `other` (default `other`) |
| `organization` | string | No | Company/organization name |
| `jobTitle` | string | No | Job title |

## Behavior

- Uses the Google People API `people.createContact` endpoint
- Creates a new contact with the provided fields
- Returns the full details of the created contact (including resource name and etag)
- Only `givenName` is required; all other fields are optional
- **Conditional registration:** Only available when `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are set in `.env`

## Implementation

- Source: `src/MicroXAgentLoop/Tools/Contacts/ContactsCreateTool.cs`
- Uses `Google.Apis.PeopleService.v1` NuGet package
- Builds a `Person` object from input fields with null checks
- Uses `ContactsFormatter.FormatContactDetail()` for the response
- OAuth2 via `ContactsAuth.Instance.GetServiceAsync()` (extends `GoogleAuthBase<PeopleServiceService>`)

## Example

```
you> Create a contact for Jane Doe at Acme Corp, email jane@acme.com, phone 07700900123
```

Claude calls:
```json
{
  "name": "contacts_create",
  "input": {
    "givenName": "Jane",
    "familyName": "Doe",
    "email": "jane@acme.com",
    "emailType": "work",
    "phone": "07700900123",
    "phoneType": "mobile",
    "organization": "Acme Corp"
  }
}
```

## Authentication

On first use, a browser window opens for Google OAuth sign-in. Tokens are cached in `.contacts-tokens/` for future sessions. See [Getting Started](../../../operations/getting-started.md) for setup instructions.
