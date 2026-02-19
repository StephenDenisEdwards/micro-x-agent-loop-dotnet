using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Contacts;

public class ContactsGetTool : GoogleToolBase
{
    public ContactsGetTool(string googleClientId, string googleClientSecret)
        : base(googleClientId, googleClientSecret) { }

    public override string Name => "contacts_get";

    public override string Description =>
        "Get full details of a Google Contact by resource name. " +
        "Returns name, emails, phones, addresses, organization, biography, and etag " +
        "(needed for updates).";

    public override JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "resourceName": {
                    "type": "string",
                    "description": "The contact's resource name (e.g. 'people/c1234567890')."
                }
            },
            "required": ["resourceName"]
        }
        """)!;

    public override async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        try
        {
            var service = await ContactsAuth.Instance.GetServiceAsync(GoogleClientId, GoogleClientSecret);
            var resourceName = input["resourceName"]!.GetValue<string>();

            var request = service.People.Get(resourceName);
            request.PersonFields = "names,emailAddresses,phoneNumbers,addresses,organizations,biographies";

            var person = await request.ExecuteAsync();

            return ContactsFormatter.FormatContactDetail(person);
        }
        catch (Exception ex)
        {
            return HandleError(ex.Message);
        }
    }
}
