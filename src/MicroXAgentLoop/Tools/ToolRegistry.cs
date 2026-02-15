using MicroXAgentLoop.Tools.Gmail;
using MicroXAgentLoop.Tools.LinkedIn;

namespace MicroXAgentLoop.Tools;

public static class ToolRegistry
{
    public static IReadOnlyList<ITool> GetAll(
        string? documentsDirectory = null,
        string? googleClientId = null,
        string? googleClientSecret = null)
    {
        var tools = new List<ITool>
        {
            new BashTool(),
            new ReadFileTool(documentsDirectory),
            new WriteFileTool(),
            new LinkedInJobsTool(),
            new LinkedInJobDetailTool(),
        };

        if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
        {
            tools.Add(new GmailSearchTool(googleClientId, googleClientSecret));
            tools.Add(new GmailReadTool(googleClientId, googleClientSecret));
            tools.Add(new GmailSendTool(googleClientId, googleClientSecret));
        }

        return tools;
    }
}
