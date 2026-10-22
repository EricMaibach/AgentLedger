namespace AgentLedger.Web.Components.Formatting;

/// <summary>Human names for agents, by their kebab-case wire name (docs/ui.md, AgentName).</summary>
public static class AgentDisplayName
{
  private static readonly Dictionary<string, string> Names = new()
  {
    ["claude-code"] = "Claude Code",
    ["codex"] = "Codex",
    ["copilot-cli"] = "Copilot CLI",
    ["vs-code-copilot"] = "VS Code Copilot",
    ["cortex-code"] = "Cortex Code",
  };

  /// <summary>The display name, or the wire name itself for an agent this list doesn't know yet.</summary>
  public static string For(string wireName) => Names.GetValueOrDefault(wireName, wireName);

  /// <summary>Every known agent, for filter choices.</summary>
  public static IReadOnlyDictionary<string, string> All => Names;
}
