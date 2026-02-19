using System.Diagnostics;
using System.Text.Json.Nodes;

namespace MicroXAgentLoop.Tools;

public class BashTool : ITool
{
    private const int CommandTimeoutMs = 30_000;

    private readonly string? _workingDirectory;

    public BashTool(string? workingDirectory = null)
    {
        _workingDirectory = workingDirectory;
    }

    public string Name => "bash";
    public string Description => "Execute a bash command and return its output (stdout + stderr).";

    public JsonNode InputSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "command": {
                    "type": "string",
                    "description": "The bash command to execute"
                }
            },
            "required": ["command"]
        }
        """)!;

    public async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        var command = input["command"]!.GetValue<string>();
        var isWindows = OperatingSystem.IsWindows();
        var fileName = isWindows ? "cmd.exe" : "bash";
        var arguments = isWindows ? $"/c {command}" : $"-c {command}";

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _workingDirectory ?? "",
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            var completed = process.WaitForExit(CommandTimeoutMs);
            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                return $"{await stdoutTask}{await stderrTask}\n[timed out after {CommandTimeoutMs / 1000}s]";
            }

            await process.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                return $"{stdout}{stderr}\n[exit code {process.ExitCode}]";
            }

            return $"{stdout}{stderr}".TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }
}
