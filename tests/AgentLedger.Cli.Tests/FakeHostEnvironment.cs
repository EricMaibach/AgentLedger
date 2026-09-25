using AgentLedger.Cli.Envelope;

namespace AgentLedger.Cli.Tests;

// Environment variables are process-wide, and xUnit runs test classes in parallel, so tests use
// this fake instead of setting real variables.
public sealed class FakeHostEnvironment : IHostEnvironment
{
  public Dictionary<string, string> Variables { get; } = [];
  public string MachineName { get; set; } = "a1b2c3d4e5f6";
  public string UserName { get; set; } = "vscode";
  public string CurrentDirectory { get; set; } = "/tmp";
  public string HomeDirectory { get; set; } = "/nonexistent-home";
  public string UserConfigDirectory { get; set; } = "/nonexistent-config";
  public string LocalDataDirectory { get; set; } = "/nonexistent-data";
  public string? ProcessPath { get; set; } = "/opt/agentledger/agentledger";

  public string? GetVariable(string name) => Variables.GetValueOrDefault(name);
  public IReadOnlyDictionary<string, string> GetVariables() => Variables;
}
