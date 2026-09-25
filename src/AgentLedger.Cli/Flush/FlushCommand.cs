using System.CommandLine;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Flush;

/// <summary>`agentledger flush`: sends every waiting event now. Exits 1 if some couldn't be sent.</summary>
internal static class FlushCommand
{
  public static Command Create(IHostEnvironment environment, TextWriter stdout)
  {
    var command = new Command("flush", "Send all events waiting in the spool now, e.g. after the API was unavailable.");

    command.SetAction(async (_, cancellationToken) =>
    {
      var settings = new SettingsLoader(environment).Load(environment.CurrentDirectory);
      var spool = new EventSpool(settings.SpoolDirectory.Value, SpoolLimits.Default);
      if (spool.Measure().Count == 0)
      {
        await stdout.WriteLineAsync("Nothing waiting.");
        return 0;
      }

      using var http = new HttpClient { Timeout = settings.Timeout.Value };
      var drainer = new SpoolDrainer(spool, new HttpEventSender(http, settings.Url.Value, settings.ApiKey.Value),
        new FileLog(CliPaths.LogFile(environment), TimeProvider.System));

      var report = await drainer.DrainAsync(limit: null, except: null, cancellationToken);
      var remaining = spool.Measure().Count;

      var rejected = report.Rejected == 0 ? "" : $", rejected {report.Rejected} as malformed (see the log)";
      await stdout.WriteLineAsync($"Sent {report.Sent}{rejected}; {remaining} still waiting.");
      if (report.StoppedBecause is { } reason)
      {
        await stdout.WriteLineAsync($"Stopped: the API is unavailable ({reason}).");
      }

      return remaining == 0 ? 0 : 1;
    });

    return command;
  }
}
