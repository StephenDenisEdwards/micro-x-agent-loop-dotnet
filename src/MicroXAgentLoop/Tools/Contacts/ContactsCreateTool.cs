using System.Text.Json.Nodes;
using Google.Apis.PeopleService.v1.Data;

namespace MicroXAgentLoop.Tools.Contacts;

public class ContactsCreateTool : GoogleToolBase
{
    public ContactsCreateTool(string googleClientId, string googleClientSecret)
        : base(googleClientId, googleClientSecret) { }

    public override string Name => "contacts_create";

    public override string Description =>
        "Create a new Google Contact. At minimum requires a given name. " +
        "Can also set family name, email, phone, organization, and job title.";

    public override JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "givenName": {
                    "type": "string",
                    "description": "First/given name (required)."
                },
                "familyName": {
                    "type": "string",
                    "description": "Last/family name."
                },
                "email": {
                    "type": "string",
                    "description": "Email address."
                },
                "emailType": {
                    "type": "string",
                    "description": "Email type: 'home', 'work', or 'other' (default 'other')."
                },
                "phone": {
                    "type": "string",
                    "description": "Phone number."
                },
                "phoneType": {
                    "type": "string",
                    "description": "Phone type: 'home', 'work', 'mobile', or 'other' (default 'other')."
                },
                "organization": {
                    "type": "string",
                    "description": "Company/organization name."
                },
                "jobTitle": {
                    "type": "string",
                    "description": "Job title."
                }
            },
            "required": ["givenName"]
        }
        """)!;

    public override async Task<string> ExecuteAsync(JsonNode input)
    {
        try
        {
            var service = await ContactsAuth.Instance.GetServiceAsync(GoogleClientId, GoogleClientSecret);

            var nameObj = new Name { GivenName = input["givenName"]!.GetValue<string>() };

            var familyName = input["familyName"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(familyName))
                nameObj.FamilyName = familyName;

            var body = new Person
            {
                Names = new List<Name> { nameObj },
            };

            var email = input["email"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(email))
            {
                var emailType = input["emailType"]?.GetValue<string>() ?? "other";
                body.EmailAddresses = new List<EmailAddress>
                {
                    new() { Value = email, Type = emailType },
                };
            }

            var phone = input["phone"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(phone))
            {
                var phoneType = input["phoneType"]?.GetValue<string>() ?? "other";
                body.PhoneNumbers = new List<PhoneNumber>
                {
                    new() { Value = phone, Type = phoneType },
                };
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
            }

            var person = await service.People.CreateContact(body).ExecuteAsync();

            return "Contact created successfully.\n\n" + ContactsFormatter.FormatContactDetail(person);
        }
        catch (Exception ex)
        {
            return HandleError(ex.Message);
        }
    }
}
