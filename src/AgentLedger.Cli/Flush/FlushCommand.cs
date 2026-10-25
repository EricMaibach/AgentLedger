using System.CommandLine;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Hook;
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
      var transcriptSpool = new EventSpool(CliPaths.TranscriptSpoolDirectory(settings.SpoolDirectory.Value), SpoolLimits.Transcripts);
      if (spool.Measure().Count == 0 && transcriptSpool.Measure().Count == 0)
      {
        await stdout.WriteLineAsync("Nothing waiting.");
        return 0;
      }

      using var http = new HttpClient { Timeout = settings.Timeout.Value };
      var log = new FileLog(CliPaths.LogFile(environment), TimeProvider.System);
      var drainer = new SpoolDrainer(spool, new HttpEventSender(http, settings.Url.Value, settings.ApiKey.Value), log);

      var report = await drainer.DrainAsync(limit: null, except: null, cancellationToken);
      var remaining = spool.Measure().Count;

      var rejected = report.Rejected == 0 ? "" : $", rejected {report.Rejected} as malformed (see the log)";
      await stdout.WriteLineAsync($"Sent {report.Sent}{rejected}; {remaining} still waiting.");
      if (report.StoppedBecause is { } reason)
      {
        await stdout.WriteLineAsync($"Stopped: the API is unavailable ({reason}).");
      }

      // Transcript batches only once events are through: the API is evidently reachable then.
      if (report.StoppedBecause is null && transcriptSpool.Measure().Count > 0)
      {
        var transcripts = HookCommand.Transcripts("flush", environment, settings, http, log);
        var transcriptReport = await transcripts.SendAsync(limit: null, cancellationToken);
        var transcriptsRemaining = transcriptSpool.Measure().Count;
        await stdout.WriteLineAsync($"Transcript batches: sent {transcriptReport.Sent}; {transcriptsRemaining} still waiting.");
        if (transcriptReport.StoppedBecause is { } transcriptReason)
        {
          await stdout.WriteLineAsync($"Stopped: {transcriptReason}.");
        }

        remaining += transcriptsRemaining;
      }
      else
      {
        remaining += transcriptSpool.Measure().Count;
      }

      return remaining == 0 ? 0 : 1;
    });

    return command;
  }
}
