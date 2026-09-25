namespace AgentLedger.Cli.Envelope;

/// <summary>
/// The little the CLI knows about each agent (ADR 0012): which top-level payload fields hold the event
/// name and session ID, and which variable holds the project directory. Adding an agent adds one entry.
/// </summary>
internal sealed record AgentProfile(string EventTypeField, string SessionIdField, string? ProjectDirVariable)
{
  // Claude Code is verified from captured payloads; Codex and Cortex Code follow Claude Code's hook
  // schema per their documentation, unverified until captured.
  private static readonly Dictionary<string, AgentProfile> Known = new()
  {
    ["claude-code"] = new("hook_event_name", "session_id", "CLAUDE_PROJECT_DIR"),
    ["codex"] = new("hook_event_name", "session_id", null),
    ["cortex-code"] = new("hook_event_name", "session_id", "CORTEX_PROJECT_DIR"),
  };

  private static readonly AgentProfile Default = new("hook_event_name", "session_id", null);

  public static AgentProfile For(string agent) => Known.GetValueOrDefault(agent, Default);

  /// <summary>The agent's project directory variable if set, otherwise the current directory.</summary>
  public string ResolveProjectDirectory(IHostEnvironment environment) =>
    (ProjectDirVariable is null ? null : environment.GetVariable(ProjectDirVariable)) is { Length: > 0 } fromAgent
      ? fromAgent
      : environment.CurrentDirectory;
}
