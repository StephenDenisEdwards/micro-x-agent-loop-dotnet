using System.Text.Json.Nodes;
using Google.Apis.PeopleService.v1;

namespace MicroXAgentLoop.Tools.Contacts;

public class ContactsListTool : ITool
{
    private readonly string _googleClientId;
    private readonly string _googleClientSecret;

    public ContactsListTool(string googleClientId, string googleClientSecret)
    {
        _googleClientId = googleClientId;
        _googleClientSecret = googleClientSecret;
    }

    public string Name => "contacts_list";

    public string Description =>
        "List Google Contacts. Returns contacts with name, email, and phone number. " +
        "Supports pagination via pageToken.";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "pageSize": {
                    "type": "number",
                    "description": "Number of contacts to return (default 10, max 100)."
                },
                "pageToken": {
                    "type": "string",
                    "description": "Page token from a previous response for pagination."
                },
                "sortOrder": {
                    "type": "string",
                    "description": "Sort order: 'LAST_MODIFIED_ASCENDING', 'LAST_MODIFIED_DESCENDING', 'FIRST_NAME_ASCENDING', or 'LAST_NAME_ASCENDING'."
                }
            },
            "required": []
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input)
    {
        try
        {
            var service = await ContactsAuth.GetContactsServiceAsync(_googleClientId, _googleClientSecret);
            var pageSize = Math.Min(input["pageSize"]?.GetValue<int>() ?? 10, 100);
            var pageToken = input["pageToken"]?.GetValue<string>();
            var sortOrder = input["sortOrder"]?.GetValue<string>();

            var request = service.People.Connections.List("people/me");
            request.PersonFields = "names,emailAddresses,phoneNumbers";
            request.PageSize = pageSize;

            if (!string.IsNullOrEmpty(pageToken))
                request.PageToken = pageToken;
            if (!string.IsNullOrEmpty(sortOrder) && Enum.TryParse<PeopleResource.ConnectionsResource.ListRequest.SortOrderEnum>(sortOrder, ignoreCase: true, out var sortOrderEnum))
                request.SortOrder = sortOrderEnum;

            var response = await request.ExecuteAsync();
            var connections = response.Connections;

            if (connections is null || connections.Count == 0)
                return "No contacts found.";

            var formatted = new List<string>();
            foreach (var person in connections)
            {
                formatted.Add(ContactsFormatter.FormatContactSummary(person));
            }

            var result = string.Join("\n\n", formatted);

            if (!string.IsNullOrEmpty(response.NextPageToken))
                result += $"\n\n--- More results available. Use pageToken: {response.NextPageToken} ---";

            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  contacts_list error: {ex.Message}");
            return $"Error listing contacts: {ex.Message}";
        }
    }
}
