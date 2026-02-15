using MicroXAgentLoop.Tools.Gmail;
using MicroXAgentLoop.Tools.LinkedIn;

namespace MicroXAgentLoop.Tools;

public static class ToolRegistry
{
    public static IReadOnlyList<ITool> GetAll(
        string? documentsDirectory = null,
        string? googleClientId = null,
        string? googleClientSecret = null) =>
    [
        new BashTool(),
        new ReadFileTool(documentsDirectory),
        new WriteFileTool(),
        new LinkedInJobsTool(),
        new LinkedInJobDetailTool(),
        new GmailSearchTool(googleClientId ?? "", googleClientSecret ?? ""),
        new GmailReadTool(googleClientId ?? "", googleClientSecret ?? ""),
        new GmailSendTool(googleClientId ?? "", googleClientSecret ?? ""),
    ];
}
