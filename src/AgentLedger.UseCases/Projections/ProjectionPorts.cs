using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;

namespace AgentLedger.UseCases.Projections;

// One session in the raw tables: the unit projections are rebuilt in (ADR 0022).
public sealed record SessionKey(AgentKind Agent, string NativeSessionId);

// Reading the raw tables for projections (query service).
public interface IProjectionSourceQuery
{
  // Everything stored for one session, or null when nothing is.
  Task<SessionEvidence?> ReadSessionAsync(SessionKey key, CancellationToken cancellationToken);

  // Sessions with raw rows whose updated_at is after `after` (all rows when null) and at most `upTo`
  // (docs/architecture.md, incremental processing).
  Task<IReadOnlyList<SessionKey>> FindChangedSessionsAsync(DateTimeOffset? after, DateTimeOffset upTo,
    CancellationToken cancellationToken);
}

// How far the projections have got: the watermark over the raw tables' updated_at, and the projection version every
// stored session was last brought up to.
public sealed record ProjectionCheckpoint(DateTimeOffset? Watermark, int AppliedVersion)
{
  public static readonly ProjectionCheckpoint Initial = new(null, 0);
}

// Writing projections (write store): a whole session at a time, replacing what was stored for it.
public interface IProjectionStore
{
  // Replaces the session's projection, in one transaction, recording the projection version that built it.
  Task ReplaceSessionAsync(ProjectedSession session, int version, CancellationToken cancellationToken);

  // Stored sessions built by an older version than `version`, least recently active first.
  Task<IReadOnlyList<SessionKey>> ListOutdatedSessionsAsync(int version, int limit, CancellationToken cancellationToken);

  Task<ProjectionCheckpoint> GetCheckpointAsync(CancellationToken cancellationToken);

  Task SaveCheckpointAsync(ProjectionCheckpoint checkpoint, CancellationToken cancellationToken);
}

// Keeps projection work to one worker, even when several API instances run (ADR 0022). Held until disposed.
public interface IProjectionLock
{
  // The lock, or null when another worker holds it.
  Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken);
}
