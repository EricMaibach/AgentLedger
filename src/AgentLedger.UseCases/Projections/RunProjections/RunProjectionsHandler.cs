using Microsoft.Extensions.Logging;

namespace AgentLedger.UseCases.Projections.RunProjections;

// One pass of the projection worker (ADR 0022): every session with raw rows in the new window is rebuilt as a whole;
// then, while sessions built by an older projection version remain, one batch of them. Sessions are independent: one
// the adapter can't project is logged and skipped. A storage failure ends the pass before the watermark moves, so the
// next pass retries the same window (rebuilding a session twice is harmless).
public sealed class RunProjectionsHandler(IProjectionSourceQuery source, IProjectionStore store, IEnumerable<IAgentAdapter> adapters,
  TimeProvider clock, ILogger<RunProjectionsHandler> logger) : ICommandHandler<RunProjectionsCommand, Result<ProjectionRunSummary>>
{
  public const int RebuildBatchSize = 50;

  private readonly Dictionary<string, IAgentAdapter> _adapters = adapters.ToDictionary(a => a.Agent.Name);

  public async ValueTask<Result<ProjectionRunSummary>> Handle(RunProjectionsCommand command, CancellationToken cancellationToken)
  {
    var checkpoint = await store.GetCheckpointAsync(cancellationToken);
    var upTo = clock.GetUtcNow() - command.Lag;
    int projected = 0, failed = 0, rebuilt = 0;

    foreach (var key in await source.FindChangedSessionsAsync(checkpoint.Watermark, upTo, cancellationToken))
    {
      switch (await ProjectAsync(key, cancellationToken))
      {
        case true: projected++; break;
        case false: failed++; break;
      }
    }

    var appliedVersion = checkpoint.AppliedVersion;
    if (appliedVersion < ProjectionVersion.Current)
    {
      var outdated = await store.ListOutdatedSessionsAsync(ProjectionVersion.Current, RebuildBatchSize, cancellationToken);
      foreach (var key in outdated)
      {
        switch (await ProjectAsync(key, cancellationToken))
        {
          case true: rebuilt++; break;
          case false: failed++; break;
        }
      }

      // A full batch may not be the last one. A session that keeps failing stays outdated and would hold this back,
      // so the version counts as applied once a batch is short.
      if (outdated.Count < RebuildBatchSize)
        appliedVersion = ProjectionVersion.Current;
    }

    await store.SaveCheckpointAsync(new ProjectionCheckpoint(upTo, appliedVersion), cancellationToken);
    return new ProjectionRunSummary(projected, failed, rebuilt);
  }

  // True when projected, false when the adapter couldn't, null when there's nothing to project.
  private async Task<bool?> ProjectAsync(SessionKey key, CancellationToken cancellationToken)
  {
    if (!_adapters.TryGetValue(key.Agent.Name, out var adapter))
      return null; // no adapter for this agent yet (ADR 0003): its raw rows wait for one

    var evidence = await source.ReadSessionAsync(key, cancellationToken);
    if (evidence is null)
      return null;

    Core.Projections.ProjectedSession session;
    try
    {
      session = adapter.Project(evidence);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
    {
      logger.LogError(ex, "Couldn't project {Agent} session {Session}", key.Agent.Name, key.NativeSessionId);
      return false;
    }

    await store.ReplaceSessionAsync(session, ProjectionVersion.Current, cancellationToken);
    return true;
  }
}
