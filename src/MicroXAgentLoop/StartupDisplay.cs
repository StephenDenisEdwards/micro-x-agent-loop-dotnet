namespace MicroXAgentLoop;

/// <summary>
/// Renders the startup banner with tool list, MCP servers, and configuration summary.
/// </summary>
public static class StartupDisplay
{
    public static void Show(
        IReadOnlyList<ITool> allTools,
        IReadOnlyList<ITool> mcpTools,
        string? workingDirectory,
        string compactionStrategy,
        int compactionThresholdTokens,
        int protectedTailMessages,
        IReadOnlyList<string> logDescriptions)
    {
        Console.WriteLine("micro-x-agent-loop (type 'exit' to quit)");

        var builtinTools = allTools.Where(t => !mcpTools.Contains(t)).ToList();
        Console.WriteLine("Tools:");
        foreach (var t in builtinTools)
            Console.WriteLine($"  - {t.Name}");

        if (mcpTools.Count > 0)
        {
            var mcpNames = new Dictionary<string, List<string>>();
            foreach (var t in mcpTools)
            {
                var parts = t.Name.Split("__", 2);
                var server = parts[0];
                var toolName = parts.Length > 1 ? parts[1] : t.Name;
                if (!mcpNames.ContainsKey(server))
                    mcpNames[server] = [];
                mcpNames[server].Add(toolName);
            }
            Console.WriteLine("MCP servers:");
            foreach (var (server, toolNames) in mcpNames)
                Console.WriteLine($"  - {server}: {string.Join(", ", toolNames)}");
        }

        if (!string.IsNullOrEmpty(workingDirectory))
            Console.WriteLine($"Working directory: {workingDirectory}");

        if (compactionStrategy != "none")
            Console.WriteLine(
                $"Compaction: {compactionStrategy} (threshold: {compactionThresholdTokens:N0} tokens, tail: {protectedTailMessages} messages)");

        if (logDescriptions.Count > 0)
            Console.WriteLine($"Logging: {string.Join(", ", logDescriptions)}");

        Console.WriteLine();
    }
}
