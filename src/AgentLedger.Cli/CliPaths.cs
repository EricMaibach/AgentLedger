namespace AgentLedger.Cli;

/// <summary>Where the CLI keeps its own files, under the OS per-user local data folder.</summary>
internal static class CliPaths
{
  public static string DataDirectory(IHostEnvironment environment) => Path.Combine(environment.LocalDataDirectory, "agentledger");

  public static string LogFile(IHostEnvironment environment) => Path.Combine(DataDirectory(environment), "agentledger.log");
}
