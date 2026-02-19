using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Contacts;

public class ContactsDeleteTool : GoogleToolBase
{
    public ContactsDeleteTool(string googleClientId, string googleClientSecret)
        : base(googleClientId, googleClientSecret) { }

    public override string Name => "contacts_delete";

    public override string Description => "Delete a Google Contact by resource name. This action cannot be undone.";

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

            await service.People.DeleteContact(resourceName).ExecuteAsync();

            return $"Contact '{resourceName}' deleted successfully.";
        }
        catch (Exception ex)
        {
            return HandleError(ex.Message);
        }
    }
}
