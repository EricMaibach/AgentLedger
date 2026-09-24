namespace AgentLedger.Core.AgentEventReceiptAggregate;

public sealed class AgentKind : SmartEnum<AgentKind>
{
  public static readonly AgentKind ClaudeCode = new(nameof(ClaudeCode), 1);
  public static readonly AgentKind Codex = new(nameof(Codex), 2);
  public static readonly AgentKind CopilotCli = new(nameof(CopilotCli), 3);
  public static readonly AgentKind CortexCode = new(nameof(CortexCode), 4);
  public static readonly AgentKind VsCodeCopilot = new(nameof(VsCodeCopilot), 5);
  private AgentKind(string name, int value) : base(name, value) { }
}
