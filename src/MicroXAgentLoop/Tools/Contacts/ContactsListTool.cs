using System.Text.Json.Nodes;
using Google.Apis.PeopleService.v1;

namespace MicroXAgentLoop.Tools.Contacts;

public class ContactsListTool : GoogleToolBase
{
    public ContactsListTool(string googleClientId, string googleClientSecret)
        : base(googleClientId, googleClientSecret) { }

    public override string Name => "contacts_list";

    public override string Description =>
        "List Google Contacts. Returns contacts with name, email, and phone number. " +
        "Supports pagination via pageToken.";

    private static readonly JsonNode Schema = JsonNode.Parse("""
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

    public override JsonNode InputSchema => Schema;

    public override async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        try
        {
            var service = await ContactsAuth.Instance.GetServiceAsync(GoogleClientId, GoogleClientSecret);
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

            var response = await request.ExecuteAsync(ct);
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
            return HandleError(ex.Message);
        }
    }
}
