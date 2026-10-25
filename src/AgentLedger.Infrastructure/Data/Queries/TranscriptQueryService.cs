using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.UseCases.Transcripts;

namespace AgentLedger.Infrastructure.Data.Queries;

// Reading transcript lines back like raw events (ADR 0020, revised): newest first, filtered, keyset-paged,
// only a short preview of each line in lists.
public sealed class TranscriptQueryService(AppDbContext db) : ITranscriptQueryService
{
  public async Task<TranscriptLinePage> ListLinesAsync(TranscriptLineFilter filter, TranscriptLineCursor? after, int limit,
    CancellationToken cancellationToken)
  {
    var lines = Filtered(filter);

    // Keyset paging on (received_at, byte_offset, id) DESC (ADR 0016). Vogen IDs don't define '<';
    // CompareTo(...) < 0 is translated to a plain SQL comparison.
    if (after is not null)
    {
      var afterId = TranscriptLineId.From(after.LineId);
      lines = lines.Where(l =>
        l.ReceivedAt < after.ReceivedAt
        || (l.ReceivedAt == after.ReceivedAt && l.ByteOffset < after.ByteOffset)
        || (l.ReceivedAt == after.ReceivedAt && l.ByteOffset == after.ByteOffset && l.Id.CompareTo(afterId) < 0));
    }

    var rows = await lines
      .OrderByDescending(l => l.ReceivedAt)
      .ThenByDescending(l => l.ByteOffset)
      .ThenByDescending(l => l.Id)
      .Take(limit + 1) // one extra row says whether another page exists, without a COUNT
      .Select(l => new
      {
        l.Id,
        l.Agent,
        l.LineType,
        l.NativeSessionId,
        l.File,
        l.SubagentId,
        l.ByteOffset,
        l.ReceivedAt,
        Preview = l.Text.Substring(0, TranscriptLineListItem.PreviewLength),
      })
      .ToListAsync(cancellationToken);

    // In memory: unwrap value objects, which EF can't translate.
    var items = rows.Take(limit)
      .Select(r => new TranscriptLineListItem(r.Id.Value, r.Agent.Name, r.LineType, r.NativeSessionId, r.File.Value, r.SubagentId,
        r.ByteOffset, r.ReceivedAt, r.Preview))
      .ToList();
    var next = rows.Count > limit ? new TranscriptLineCursor(items[^1].ReceivedAt, items[^1].ByteOffset, items[^1].LineId) : null;
    return new TranscriptLinePage(items, next);
  }

  public async Task<TranscriptLineRow?> GetLineAsync(TranscriptLineId lineId, CancellationToken cancellationToken)
  {
    var row = await (
        from l in db.TranscriptLines.AsNoTracking()
        where l.Id == lineId
        join f in db.TranscriptFiles on new { l.Agent, l.NativeSessionId, l.File } equals new { f.Agent, f.NativeSessionId, f.File } into files
        from f in files.DefaultIfEmpty()
        select new
        {
          l.Id,
          l.Agent,
          l.LineType,
          l.NativeSessionId,
          l.File,
          l.SubagentId,
          l.ByteOffset,
          l.Text,
          l.ReceivedAt,
          l.ApiKeyId,
          Meta = f == null ? null : f.Meta,
        })
      .SingleOrDefaultAsync(cancellationToken);

    return row is null
      ? null
      : new TranscriptLineRow(row.Id.Value, row.Agent.Name, row.LineType, row.NativeSessionId, row.File.Value, row.SubagentId,
          row.ByteOffset, row.Text, row.ReceivedAt, row.ApiKeyId.Value, row.Meta);
  }

  private IQueryable<TranscriptLine> Filtered(TranscriptLineFilter filter)
  {
    var lines = db.TranscriptLines.AsNoTracking();

    if (filter.Agent is not null)
    {
      // Convert the value, not the column: EF compares the stored name with this one.
      var agent = AgentKind.FromName(filter.Agent);
      lines = lines.Where(l => l.Agent == agent);
    }

    if (filter.LineType is not null)
    {
      lines = lines.Where(l => l.LineType == filter.LineType);
    }

    if (filter.NativeSessionId is not null)
    {
      lines = lines.Where(l => l.NativeSessionId == filter.NativeSessionId);
    }

    if (filter.File is not null)
    {
      var file = TranscriptFileName.From(filter.File);
      lines = lines.Where(l => l.File == file);
    }

    // Received-time range: From inclusive, To exclusive, as for raw events.
    if (filter.From is { } from)
    {
      lines = lines.Where(l => l.ReceivedAt >= from);
    }

    if (filter.To is { } to)
    {
      lines = lines.Where(l => l.ReceivedAt < to);
    }

    return lines;
  }
}
