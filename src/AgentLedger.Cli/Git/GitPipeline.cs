using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Git;

/// <summary>Recording new commits into their spool, and sending what's waiting there (ADR 0021).</summary>
internal sealed class GitPipeline(GitCatchUp catchUp, SpoolDrainer drainer)
{
  /// <summary>Batches sent per hook run; `flush` sends the rest.</summary>
  public const int SendLimitPerHook = 3;

  public Task CatchUpAsync(string projectDirectory) => catchUp.RunAsync(projectDirectory);

  public Task<DrainReport> SendAsync(int? limit, CancellationToken cancellationToken) => drainer.DrainAsync(limit, except: null, cancellationToken);
}
