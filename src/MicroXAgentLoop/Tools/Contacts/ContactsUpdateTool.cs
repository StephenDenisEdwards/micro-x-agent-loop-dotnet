using System.Text.Json.Nodes;
using Google.Apis.PeopleService.v1.Data;

namespace MicroXAgentLoop.Tools.Contacts;

public class ContactsUpdateTool : ITool
{
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;

    public ContactsUpdateTool(string googleClientId, string googleClientSecret)
    {
        _googleClientId = googleClientId;
        _googleClientSecret = googleClientSecret;
    }

    public string Name => "contacts_update";

    public string Description =>
        "Update an existing Google Contact. Requires the resource name and etag " +
        "(from contacts_get). Provide only the fields you want to change.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "resourceName": {
                    "type": "string",
                    "description": "The contact's resource name (e.g. 'people/c1234567890')."
                },
                "etag": {
                    "type": "string",
                    "description": "The contact's etag (from contacts_get, required for concurrency control)."
                },
                "givenName": {
                    "type": "string",
                    "description": "New first/given name."
                },
                "familyName": {
                    "type": "string",
                    "description": "New last/family name."
                },
                "email": {
                    "type": "string",
                    "description": "New email address (replaces existing emails)."
                },
                "emailType": {
                    "type": "string",
                    "description": "Email type: 'home', 'work', or 'other' (default 'other')."
                },
                "phone": {
                    "type": "string",
                    "description": "New phone number (replaces existing phones)."
                },
                "phoneType": {
                    "type": "string",
                    "description": "Phone type: 'home', 'work', 'mobile', or 'other' (default 'other')."
                },
                "organization": {
                    "type": "string",
                    "description": "New company/organization name."
                },
                "jobTitle": {
                    "type": "string",
                    "description": "New job title."
                }
            },
            "required": ["resourceName", "etag"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        try
        {
            var service = await ContactsAuth.GetContactsServiceAsync(_googleClientId, _googleClientSecret);
            var resourceName = input["resourceName"]!.GetValue<string>();

            var body = new Person
            {
                ETag = input["etag"]!.GetValue<string>(),
            };

            var updateFields = new List<string>();

            var givenName = input["givenName"]?.GetValue<string>();
            var familyName = input["familyName"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(givenName) || !string.IsNullOrEmpty(familyName))
            {
                var nameObj = new Name();
                if (!string.IsNullOrEmpty(givenName))
                    nameObj.GivenName = givenName;
                if (!string.IsNullOrEmpty(familyName))
                    nameObj.FamilyName = familyName;
                body.Names = new List<Name> { nameObj };
                updateFields.Add("names");
            }

            var email = input["email"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(email))
            {
                var emailType = input["emailType"]?.GetValue<string>() ?? "other";
                body.EmailAddresses = new List<EmailAddress>
                {
                    new() { Value = email, Type = emailType },
                };
                updateFields.Add("emailAddresses");
            }

            var phone = input["phone"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(phone))
            {
                var phoneType = input["phoneType"]?.GetValue<string>() ?? "other";
                body.PhoneNumbers = new List<PhoneNumber>
                {
                    new() { Value = phone, Type = phoneType },
                };
                updateFields.Add("phoneNumbers");
            }

            var organization = input["organization"]?.GetValue<string>();
            var jobTitle = input["jobTitle"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(organization) || !string.IsNullOrEmpty(jobTitle))
            {
                var org = new Organization();
                if (!string.IsNullOrEmpty(organization))
                    org.Name = organization;
                if (!string.IsNullOrEmpty(jobTitle))
                    org.Title = jobTitle;
                body.Organizations = new List<Organization> { org };
                updateFields.Add("organizations");
            }

            if (updateFields.Count == 0)
                return "No fields to update. Provide at least one field to change.";

            var request = service.People.UpdateContact(body, resourceName);
            request.UpdatePersonFields = string.Join(",", updateFields);

            var person = await request.ExecuteAsync();

            return "Contact updated successfully.\n\n" + ContactsFormatter.FormatContactDetail(person);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  contacts_update error: {ex.Message}");
            return $"Error updating contact: {ex.Message}";
        }
    }
}
