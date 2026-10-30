using System.CommandLine;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Install;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Status;

/// <summary>`agentledger status`: for people checking the setup. Exits 1 when the API isn't reachable.</summary>
internal static class StatusCommand
{
  public static Command Create(IHostEnvironment environment, TextWriter stdout)
  {
    var command = new Command("status", "Show the configuration (and where each value came from), whether the API is reachable, waiting events and recent log lines.");

    command.SetAction(async (_, _) => await RunAsync(environment, stdout));

    return command;
  }

  /// <summary>Shown by `status`, and at the end of `setup`. Returns 0 when the API is reachable.</summary>
  public static async Task<int> RunAsync(IHostEnvironment environment, TextWriter stdout)
  {
    var settings = new SettingsLoader(environment).Load(environment.CurrentDirectory);
    var tracker = new LedgerVersionTracker();
    using var http = CliHttp.Create(settings.Timeout.Value, tracker);
    var api = await StatusReport.CheckApiAsync(http, settings.Url.Value);
    var key = api.Reachable && settings.ApiKey.Value is { } apiKey
      ? await Login.ApiKeyCheck.CheckAsync(http, settings.Url.Value, apiKey)
      : null;
    var spool = new EventSpool(settings.SpoolDirectory.Value, SpoolLimits.Default).Measure();
    var transcriptSpool = new EventSpool(CliPaths.TranscriptSpoolDirectory(settings.SpoolDirectory.Value), SpoolLimits.Transcripts).Measure();
    var trackedTranscripts = new Transcripts.TranscriptStateStore(CliPaths.TranscriptStateDirectory(environment)).Count();
    var gitSpool = new EventSpool(CliPaths.GitSpoolDirectory(settings.SpoolDirectory.Value), SpoolLimits.Default).Measure();
    var watchedRepositories = new Git.GitRepositoryStateStore(CliPaths.GitStateDirectory(environment)).Count();
    var log = new FileLog(CliPaths.LogFile(environment), TimeProvider.System);

    var installedScopes = Enum.GetValues<InstallScope>()
      .Where(scope => ClaudeCodeHookSettings.IsInstalled(InstallCommands.SettingsFile(scope, environment)))
      .Select(scope => scope.ToString().ToLowerInvariant())
      .ToList();

    var version = new VersionCheck(tracker.LedgerVersion, Setup.InstalledBinary.IsRunning(environment));
    await stdout.WriteAsync(StatusReport.Render(settings, api, key, spool, transcriptSpool, trackedTranscripts, gitSpool, watchedRepositories,
      log.Path, log.ReadLast(5), installedScopes, version));
    return api.Reachable ? 0 : 1;
  }
}
