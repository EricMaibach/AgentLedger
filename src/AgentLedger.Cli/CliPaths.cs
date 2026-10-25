namespace AgentLedger.Cli;

/// <summary>Where the CLI keeps its own files, under the OS per-user local data folder.</summary>
internal static class CliPaths
{
  public static string DataDirectory(IHostEnvironment environment) => Path.Combine(environment.LocalDataDirectory, "agentledger");

  public static string LogFile(IHostEnvironment environment) => Path.Combine(DataDirectory(environment), "agentledger.log");

  /// <summary>Where each transcript was read up to (ADR 0020).</summary>
  public static string TranscriptStateDirectory(IHostEnvironment environment) => Path.Combine(DataDirectory(environment), "transcripts");

  /// <summary>Transcript batches waiting to be sent: a folder inside the spool, apart from events.</summary>
  public static string TranscriptSpoolDirectory(string spoolDirectory) => Path.Combine(spoolDirectory, "transcripts");
}
