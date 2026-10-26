namespace AgentLedger.Cli.Envelope;

/// <summary>
/// The name the CLI gives this machine, in events and for repositories without a remote (ADR 0021):
/// AGENTLEDGER_HOST when set (containers often have random host names), otherwise the machine name.
/// </summary>
internal static class HostName
{
  public static string For(IHostEnvironment environment) =>
    environment.GetVariable("AGENTLEDGER_HOST") is { } configured && !string.IsNullOrWhiteSpace(configured) ? configured.Trim() : environment.MachineName;
}
