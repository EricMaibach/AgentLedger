using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.Core.Projections;

// The raw rows a projected item was built from (ADR 0015, ADR 0022): hook events by event ID (every receipt of the
// event is evidence) and transcript lines. Never empty: an item without evidence couldn't be checked.
public sealed record Lineage
{
  public IReadOnlyList<AgentEventId> EventIds { get; }
  public IReadOnlyList<TranscriptLineId> LineIds { get; }

  public Lineage(IEnumerable<AgentEventId> eventIds, IEnumerable<TranscriptLineId> lineIds)
  {
    EventIds = [.. Guard.Against.Null(eventIds).Distinct()];
    LineIds = [.. Guard.Against.Null(lineIds).Distinct()];
    if (EventIds.Count == 0 && LineIds.Count == 0)
      throw new ArgumentException("Lineage needs at least one source.", nameof(eventIds));
  }

  public static Lineage Combine(IEnumerable<Lineage> parts)
  {
    var list = Guard.Against.Null(parts).ToList();
    return new Lineage(list.SelectMany(p => p.EventIds), list.SelectMany(p => p.LineIds));
  }
}
