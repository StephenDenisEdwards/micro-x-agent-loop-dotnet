# Tool: append_file

Append content to an existing file.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | File path (absolute or relative to `WorkingDirectory`) |
| `content` | string | Yes | Content to append |

## Behavior

- Appends the provided content to the end of the specified file
- The file must already exist — use `write_file` to create it first
- Relative paths are resolved against the configured `WorkingDirectory`
- **Always registered** — no API key required

## Implementation

- Source: `src/MicroXAgentLoop/Tools/AppendFileTool.cs`
- Uses `File.AppendAllTextAsync()`

## Example

```
you> Append a new section to my notes.txt file
```
