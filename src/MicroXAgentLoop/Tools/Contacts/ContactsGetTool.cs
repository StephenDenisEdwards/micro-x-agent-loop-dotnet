using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Contacts;

public class ContactsGetTool : ITool
{
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;

    public ContactsGetTool(string googleClientId, string googleClientSecret)
    {
        _googleClientId = googleClientId;
        _googleClientSecret = googleClientSecret;
    }

    public string Name => "contacts_get";

    public string Description =>
        "Get full details of a Google Contact by resource name. " +
        "Returns name, emails, phones, addresses, organization, biography, and etag " +
        "(needed for updates).";

    public JsonNode InputSchema => JsonNode.Parse("""
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

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        try
        {
            var service = await ContactsAuth.GetContactsServiceAsync(_googleClientId, _googleClientSecret);
            var resourceName = input["resourceName"]!.GetValue<string>();

            var request = service.People.Get(resourceName);
            request.PersonFields = "names,emailAddresses,phoneNumbers,addresses,organizations,biographies";

            var person = await request.ExecuteAsync();

            return ContactsFormatter.FormatContactDetail(person);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  contacts_get error: {ex.Message}");
            return $"Error getting contact: {ex.Message}";
        }
    }
}
