namespace AgentLedger.Cli;

/// <summary>Where the CLI keeps its own files, under the OS per-user local data folder.</summary>
internal static class CliPaths
{
  public static string DataDirectory(IHostEnvironment environment) => Path.Combine(environment.LocalDataDirectory, "agentledger");

  public static string LogFile(IHostEnvironment environment) => Path.Combine(DataDirectory(environment), "agentledger.log");

  /// <summary>Where each transcript was read up to (ADR 0020).</summary>
  public static string TranscriptStateDirectory(IHostEnvironment environment) => Path.Combine(DataDirectory(environment), "transcripts");

  /// <summary>What the CLI last saw in each git working tree (ADR 0021).</summary>
  public static string GitStateDirectory(IHostEnvironment environment) => Path.Combine(DataDirectory(environment), "git");

  /// <summary>Observed commits waiting to be sent: a folder inside the spool.</summary>
  public static string GitSpoolDirectory(string spoolDirectory) => Path.Combine(spoolDirectory, "git");

  /// <summary>Transcript batches waiting to be sent: a folder inside the spool, apart from events.</summary>
  public static string TranscriptSpoolDirectory(string spoolDirectory) => Path.Combine(spoolDirectory, "transcripts");
}
