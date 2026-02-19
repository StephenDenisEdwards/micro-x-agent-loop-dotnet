# Tool: bash

Execute shell commands on the local machine.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `command` | string | Yes | The bash command to execute |

## Behavior

- **Windows:** Runs via `cmd.exe /c <command>`
- **macOS/Linux:** Runs via system shell
- **Timeout:** 30 seconds — process is killed if exceeded
- **Output:** Returns combined stdout + stderr
- **Exit code:** Non-zero exit codes are appended to the output
- **Working directory:** Uses `WorkingDirectory` from `appsettings.json` if configured

## Implementation

- Source: `src/MicroXAgentLoop/Tools/BashTool.cs`
- Uses `System.Diagnostics.Process` for process execution
- Timeout enforced via `CancellationTokenSource`
- On timeout, the process is killed and `[timed out after 30s]` is returned

## Example

```
you> List all C# files in the current directory
```

Claude calls:
```json
{
  "name": "bash",
  "input": { "command": "dir *.cs /s" }
}
```

## Security

This tool executes arbitrary shell commands by design. The user accepts full responsibility for commands the agent runs.
