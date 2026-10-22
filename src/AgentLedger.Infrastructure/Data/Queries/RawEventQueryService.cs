using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

namespace AgentLedger.Infrastructure.Data.Queries;

// The raw event list's read model (ADR 0016): a hand-shaped EF query that returns rows for the list,
// never loading full entities (payloads can be large; only a short preview is selected).
public sealed class RawEventQueryService(AppDbContext db) : IRawEventQueryService
{
  public async Task<RawEventPage> ListAsync(RawEventFilter filter, bool distinct, RawEventCursor? after, int limit,
    CancellationToken cancellationToken)
  {
    var receipts = Filtered(filter);

    // One row per event: keep a receipt only if no earlier receipt of the same event exists (its
    // first receipt; resends are hidden). Ties on time are broken by receipt ID, as everywhere else.
    if (distinct)
    {
      receipts = receipts.Where(r => !db.AgentEventReceipts.Any(earlier =>
        earlier.EventId == r.EventId
        && (earlier.ReceivedAt < r.ReceivedAt
            || (earlier.ReceivedAt == r.ReceivedAt && earlier.Id.CompareTo(r.Id) < 0))));
    }

    // Keyset paging: rows strictly after the cursor in (received_at DESC, id DESC) order.
    // Vogen IDs don't define '<'; CompareTo(...) < 0 is translated to a plain SQL comparison.
    if (after is not null)
    {
      var afterId = ReceiptId.From(after.ReceiptId);
      receipts = receipts.Where(r =>
        r.ReceivedAt < after.ReceivedAt
        || (r.ReceivedAt == after.ReceivedAt && r.Id.CompareTo(afterId) < 0));
    }

    // In SQL: only the list's columns, a receipt count per event, and the first characters of the
    // payload (cast to text: Postgres has no substring for json).
    var rows = await receipts
      .OrderByDescending(r => r.ReceivedAt)
      .ThenByDescending(r => r.Id)
      .Take(limit + 1) // one extra row says whether another page exists, without a COUNT
      .Select(r => new
      {
        r.Id,
        r.EventId,
        r.Agent,
        r.EventType,
        r.NativeSessionId,
        r.CapturedAt,
        r.ReceivedAt,
        r.Context.Host,
        r.Context.User,
        r.Context.GitRepo,
        r.Context.GitBranch,
        ReceiptCount = db.AgentEventReceipts.Count(other => other.EventId == r.EventId),
        Preview = Convert.ToString(r.Payload).Substring(0, RawEventListItem.PreviewLength),
      })
      .ToListAsync(cancellationToken);

    // In memory: unwrap value objects, which EF can't translate.
    var items = rows
      .Take(limit)
      .Select(r => new RawEventListItem(
        r.Id.Value, r.EventId.Value, r.Agent.Name, r.EventType, r.NativeSessionId, r.CapturedAt, r.ReceivedAt,
        r.Host, r.User, r.GitRepo, r.GitBranch, r.ReceiptCount, r.Preview))
      .ToList();

    var next = rows.Count > limit
      ? new RawEventCursor(items[^1].ReceivedAt, items[^1].ReceiptId)
      : null;

    return new RawEventPage(items, next);
  }

  public async Task<IReadOnlyList<EventTypeCount>> CountEventTypesAsync(RawEventFilter filter, CancellationToken cancellationToken)
  {
    // Counts events, not receipts: resends of an event don't count twice.
    var counts = await Filtered(filter)
      .GroupBy(r => r.EventType)
      .Select(group => new EventTypeCount(group.Key, group.Select(r => r.EventId).Distinct().Count()))
      .ToListAsync(cancellationToken);

    // One row per event type: few enough to order in memory, most frequent first.
    return [.. counts.OrderByDescending(c => c.Count).ThenBy(c => c.EventType, StringComparer.Ordinal)];
  }

  private IQueryable<AgentEventReceipt> Filtered(RawEventFilter filter)
  {
    var receipts = db.AgentEventReceipts.AsNoTracking();

    if (filter.Agent is not null)
    {
      // Convert the value, not the column: EF compares the stored name with this one.
      var agent = AgentKind.FromName(filter.Agent);
      receipts = receipts.Where(r => r.Agent == agent);
    }

    if (filter.EventType is not null)
    {
      receipts = receipts.Where(r => r.EventType == filter.EventType);
    }

    if (filter.NativeSessionId is not null)
    {
      receipts = receipts.Where(r => r.NativeSessionId == filter.NativeSessionId);
    }

    if (filter.Host is not null)
    {
      receipts = receipts.Where(r => r.Context.Host == filter.Host);
    }

    if (filter.GitRepo is not null)
    {
      receipts = receipts.Where(r => r.Context.GitRepo == filter.GitRepo);
    }

    // Received-time range: From inclusive, To exclusive, so consecutive ranges never overlap.
    if (filter.From is { } from)
    {
      receipts = receipts.Where(r => r.ReceivedAt >= from);
    }

    if (filter.To is { } to)
    {
      receipts = receipts.Where(r => r.ReceivedAt < to);
    }

    return receipts;
  }
}
