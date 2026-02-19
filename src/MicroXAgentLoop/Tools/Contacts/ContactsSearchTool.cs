using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools.Contacts;

public class ContactsSearchTool : ITool
{
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;

    public ContactsSearchTool(string googleClientId, string googleClientSecret)
    {
        _googleClientId = googleClientId;
        _googleClientSecret = googleClientSecret;
    }

    public string Name => "contacts_search";

    public string Description =>
        "Search Google Contacts by name, email, phone number, or other fields. " +
        "Returns matching contacts with name, email, and phone number.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Search query (name, email, phone number, etc.)."
                },
                "pageSize": {
                    "type": "number",
                    "description": "Max number of results (default 10, max 30)."
                }
            },
            "required": ["query"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        try
        {
            var service = await ContactsAuth.GetContactsServiceAsync(_googleClientId, _googleClientSecret);
            var query = input["query"]!.GetValue<string>();
            var pageSize = Math.Min(input["pageSize"]?.GetValue<int>() ?? 10, 30);

            var request = service.People.SearchContacts();
            request.Query = query;
            request.ReadMask = "names,emailAddresses,phoneNumbers";
            request.PageSize = pageSize;

            var response = await request.ExecuteAsync();
            var results = response.Results;

            if (results is null || results.Count == 0)
                return "No contacts found matching your query.";

            var formatted = new List<string>();
            foreach (var r in results)
            {
                if (r.Person is not null)
                    formatted.Add(ContactsFormatter.FormatContactSummary(r.Person));
            }

            return string.Join("\n\n", formatted);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  contacts_search error: {ex.Message}");
            return $"Error searching contacts: {ex.Message}";
        }
    }
}
