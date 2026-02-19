using MicroXAgentLoop.Tools.Anthropic;
using MicroXAgentLoop.Tools.Calendar;
using MicroXAgentLoop.Tools.Contacts;
using MicroXAgentLoop.Tools.Gmail;
using MicroXAgentLoop.Tools.LinkedIn;
using MicroXAgentLoop.Tools.Web;

namespace MicroXAgentLoop.Tools;

public static class ToolRegistry
{
    public static IReadOnlyList<ITool> GetAll(
        string? workingDirectory = null,
        string? googleClientId = null,
        string? googleClientSecret = null,
        string? anthropicAdminApiKey = null,
        string? braveApiKey = null)
    {
        var tools = new List<ITool>
        {
            new BashTool(workingDirectory),
            new ReadFileTool(workingDirectory),
            new WriteFileTool(workingDirectory),
            new AppendFileTool(workingDirectory),
            new LinkedInJobsTool(),
            new LinkedInJobDetailTool(),
            new WebFetchTool(),
        };

        if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
        {
            tools.Add(new GmailSearchTool(googleClientId, googleClientSecret));
            tools.Add(new GmailReadTool(googleClientId, googleClientSecret));
            tools.Add(new GmailSendTool(googleClientId, googleClientSecret));
            tools.Add(new CalendarListEventsTool(googleClientId, googleClientSecret));
            tools.Add(new CalendarCreateEventTool(googleClientId, googleClientSecret));
            tools.Add(new CalendarGetEventTool(googleClientId, googleClientSecret));
            tools.Add(new ContactsSearchTool(googleClientId, googleClientSecret));
            tools.Add(new ContactsListTool(googleClientId, googleClientSecret));
            tools.Add(new ContactsGetTool(googleClientId, googleClientSecret));
            tools.Add(new ContactsCreateTool(googleClientId, googleClientSecret));
            tools.Add(new ContactsUpdateTool(googleClientId, googleClientSecret));
            tools.Add(new ContactsDeleteTool(googleClientId, googleClientSecret));
        }

        if (!string.IsNullOrEmpty(anthropicAdminApiKey))
        {
            tools.Add(new AnthropicUsageTool(anthropicAdminApiKey));
        }

        if (!string.IsNullOrEmpty(braveApiKey))
        {
            tools.Add(new WebSearchTool(new BraveSearchProvider(braveApiKey)));
        }

        return tools;
    }
}
