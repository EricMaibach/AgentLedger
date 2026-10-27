using System.Text.Json;

namespace AgentLedger.UseCases.Projections.ClaudeCode;

// The argument that sums up a Claude Code tool call in one line (ADR 0022): what a Bash call ran, which file a Read,
// Edit or Write touched, what a search looked for. Other tools (MCP tools, tools added later): their first string
// argument.
internal static class ClaudeCodeToolSummary
{
  private static readonly Dictionary<string, string> KeyArgument = new()
  {
    ["Bash"] = "command",
    ["Read"] = "file_path",
    ["Edit"] = "file_path",
    ["MultiEdit"] = "file_path",
    ["Write"] = "file_path",
    ["NotebookEdit"] = "notebook_path",
    ["Grep"] = "pattern",
    ["Glob"] = "pattern",
    ["WebFetch"] = "url",
    ["WebSearch"] = "query",
    ["Agent"] = "description",
    ["Task"] = "description",
    ["Skill"] = "skill",
    ["ToolSearch"] = "query",
  };

  public static string? Of(string tool, string? input)
  {
    if (input is null)
      return null;
    try
    {
      using var json = JsonDocument.Parse(input);
      var root = json.RootElement;
      if (root.ValueKind != JsonValueKind.Object)
        return null;
      if (KeyArgument.TryGetValue(tool, out var key))
        return ClaudeCodeTranscript.String(root, key);
      return root.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String)
        .Select(p => p.Value.GetString()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
    catch (JsonException)
    {
      return null;
    }
  }
}
