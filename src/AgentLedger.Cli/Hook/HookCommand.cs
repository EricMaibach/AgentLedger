using System.CommandLine;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Envelope;
using AgentLedger.Cli.Git;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Spooling;
using AgentLedger.Cli.Transcripts;

namespace AgentLedger.Cli.Hook;

/// <summary>
/// `agentledger hook &lt;agent&gt;`: called by agent hooks with the event payload on stdin. Silent and
/// always exits 0, even when called wrongly (see <see cref="CliApp"/>): agents treat hook output as
/// instructions and some exit codes as "block this action" (ADR 0012). Problems go to the log file.
/// </summary>
internal static class HookCommand
{
  public const string Name = "hook";

  public static Command Create(IHostEnvironment environment, TextReader stdin)
  {
    var agent = new Argument<string>("agent") { Description = "The agent's kebab-case name, e.g. claude-code." };
    var command = new Command(Name, "Record one hook event from stdin. Called by agents' hooks: silent, always exits 0.") { agent };

    command.SetAction(async (parseResult, cancellationToken) =>
    {
      await RunAsync(parseResult.GetValue(agent)!, environment, stdin, cancellationToken);
      return 0;
    });

    return command;
  }

  public static TranscriptPipeline Transcripts(string agent, IHostEnvironment environment, CliSettings settings, HttpClient http, ICliLog log)
  {
    var spool = new EventSpool(CliPaths.TranscriptSpoolDirectory(settings.SpoolDirectory.Value), SpoolLimits.Transcripts);
    return new TranscriptPipeline(
      new TranscriptCatchUp(agent, TranscriptSources.For(agent), new TranscriptStateStore(CliPaths.TranscriptStateDirectory(environment)),
        new TranscriptReader(TranscriptReader.DefaultMaxChunkBytes, TranscriptReader.DefaultMaxLineBytes, log), spool, log),
      new SpoolDrainer(spool, new HttpTranscriptSender(http, settings.Url.Value, settings.ApiKey.Value), log, itemName: "transcript batch"));
  }

  public static GitPipeline Git(IHostEnvironment environment, CliSettings settings, HttpClient http, ICliLog log)
  {
    var spool = new EventSpool(CliPaths.GitSpoolDirectory(settings.SpoolDirectory.Value), SpoolLimits.Default);
    return new GitPipeline(
      new GitCommitCatchUp(new ProcessGitRunner(), new GitRepositoryStateStore(CliPaths.GitStateDirectory(environment)), spool, log,
        TimeProvider.System, HostName.For(environment)),
      new SpoolDrainer(spool, new HttpGitSender(http, settings.Url.Value, settings.ApiKey.Value), log, itemName: "git batch"));
  }

  private static async Task RunAsync(string agent, IHostEnvironment environment, TextReader stdin, CancellationToken cancellationToken)
  {
    var clock = TimeProvider.System;
    var log = new FileLog(CliPaths.LogFile(environment), clock);

    try
    {
      var payload = await stdin.ReadToEndAsync(cancellationToken);
      var settings = new SettingsLoader(environment).Load(AgentProfile.For(agent).ResolveProjectDirectory(environment));
      if (!settings.Enabled.Value)
      {
        return; // this project opted out: record nothing, not even in the spool (ADR 0013)
      }

      using var http = new HttpClient { Timeout = settings.Timeout.Value };
      var runner = new HookRunner(
        new EnvelopeBuilder(environment, clock),
        new EventSpool(settings.SpoolDirectory.Value, SpoolLimits.Default),
        new HttpEventSender(http, settings.Url.Value, settings.ApiKey.Value),
        log,
        transcripts: Transcripts(agent, environment, settings, http, log),
        git: Git(environment, settings, http, log),
        projectDirectory: AgentProfile.For(agent).ResolveProjectDirectory(environment));

      await runner.RunAsync(agent, payload, cancellationToken);
    }
    catch (Exception ex)
    {
      log.Error($"hook {agent} failed before sending: {ex}");
    }
  }
}
