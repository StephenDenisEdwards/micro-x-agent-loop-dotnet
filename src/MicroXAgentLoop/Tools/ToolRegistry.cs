using MicroXAgentLoop.Tools.Gmail;
using MicroXAgentLoop.Tools.LinkedIn;

namespace MicroXAgentLoop.Tools;

public static class ToolRegistry
{
    public static IReadOnlyList<ITool> GetAll() =>
    [
        new BashTool(),
        new ReadFileTool(),
        new WriteFileTool(),
        new LinkedInJobsTool(),
        new LinkedInJobDetailTool(),
        new GmailSearchTool(),
        new GmailReadTool(),
        new GmailSendTool(),
    ];
}
