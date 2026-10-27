using AgentLedger.UseCases.Projections;

namespace AgentLedger.Infrastructure.Data.Queries;

// Reads the raw tables for projections (ADR 0022): one session's evidence, and which sessions have new rows.
public sealed class ProjectionSourceQuery(AppDbContext db) : IProjectionSourceQuery
{
  public async Task<SessionEvidence?> ReadSessionAsync(SessionKey key, CancellationToken cancellationToken)
  {
    var receipts = await db.AgentEventReceipts.AsNoTracking()
      .Where(r => r.Agent == key.Agent && r.NativeSessionId == key.NativeSessionId)
      .ToListAsync(cancellationToken);
    var lines = await db.TranscriptLines.AsNoTracking()
      .Where(l => l.Agent == key.Agent && l.NativeSessionId == key.NativeSessionId)
      .OrderBy(l => l.File).ThenBy(l => l.ByteOffset)
      .ToListAsync(cancellationToken);
    return receipts.Count == 0 && lines.Count == 0 ? null : new SessionEvidence(key.Agent, key.NativeSessionId, receipts, lines);
  }

  public async Task<IReadOnlyList<SessionKey>> FindChangedSessionsAsync(DateTimeOffset? after, DateTimeOffset upTo,
    CancellationToken cancellationToken)
  {
    var receipts = await db.AgentEventReceipts.AsNoTracking()
      .Where(r => r.NativeSessionId != null
                  && (after == null || EF.Property<DateTimeOffset>(r, AppDbContext.UpdatedAt) > after)
                  && EF.Property<DateTimeOffset>(r, AppDbContext.UpdatedAt) <= upTo)
      .Select(r => new { r.Agent, NativeSessionId = r.NativeSessionId! })
      .Distinct()
      .ToListAsync(cancellationToken);
    var lines = await db.TranscriptLines.AsNoTracking()
      .Where(l => (after == null || EF.Property<DateTimeOffset>(l, AppDbContext.UpdatedAt) > after)
                  && EF.Property<DateTimeOffset>(l, AppDbContext.UpdatedAt) <= upTo)
      .Select(l => new { l.Agent, l.NativeSessionId })
      .Distinct()
      .ToListAsync(cancellationToken);
    return [.. receipts.Concat(lines).Distinct().Select(s => new SessionKey(s.Agent, s.NativeSessionId))];
  }
}
