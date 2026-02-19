using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Contacts;

public class ContactsDeleteTool : ITool
{
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;

    public ContactsDeleteTool(string googleClientId, string googleClientSecret)
    {
        _googleClientId = googleClientId;
        _googleClientSecret = googleClientSecret;
    }

    public string Name => "contacts_delete";

    public string Description => "Delete a Google Contact by resource name. This action cannot be undone.";

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

            await service.People.DeleteContact(resourceName).ExecuteAsync();

            return $"Contact '{resourceName}' deleted successfully.";
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  contacts_delete error: {ex.Message}");
            return $"Error deleting contact: {ex.Message}";
        }
    }
}
