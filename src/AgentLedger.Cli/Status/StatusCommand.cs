using System.CommandLine;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Install;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Status;

/// <summary>`agentledger status`: for people checking the setup. Exits 1 when the API isn't reachable.</summary>
internal static class StatusCommand
{
  public static Command Create(IHostEnvironment environment, TextWriter stdout)
  {
    var command = new Command("status", "Show the configuration (and where each value came from), whether the API is reachable, waiting events and recent log lines.");

    command.SetAction(async (_, _) =>
    {
      var settings = new SettingsLoader(environment).Load(environment.CurrentDirectory);
      using var http = new HttpClient { Timeout = settings.Timeout.Value };
      var api = await StatusReport.CheckApiAsync(http, settings.Url.Value);
      var spool = new EventSpool(settings.SpoolDirectory.Value, SpoolLimits.Default).Measure();
      var log = new FileLog(CliPaths.LogFile(environment), TimeProvider.System);

      var installedScopes = Enum.GetValues<InstallScope>()
        .Where(scope => ClaudeCodeHookSettings.IsInstalled(InstallCommands.SettingsFile(scope, environment)))
        .Select(scope => scope.ToString().ToLowerInvariant())
        .ToList();

      await stdout.WriteAsync(StatusReport.Render(settings, api, spool, log.Path, log.ReadLast(5), installedScopes));
      return api.Reachable ? 0 : 1;
    });

    return command;
  }
}
