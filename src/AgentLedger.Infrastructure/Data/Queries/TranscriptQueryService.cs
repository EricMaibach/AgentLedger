using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.UseCases.Transcripts;

namespace AgentLedger.Infrastructure.Data.Queries;

// Reading transcripts back (ADR 0020).
public sealed class TranscriptQueryService(AppDbContext db) : ITranscriptQueryService
{
  public async Task<IReadOnlyList<TranscriptFileItem>> ListFilesAsync(string nativeSessionId, CancellationToken cancellationToken)
  {
    var main = TranscriptFileName.Main;
    var rows = await db.TranscriptFiles.AsNoTracking()
      .Where(f => f.NativeSessionId == nativeSessionId)
      .OrderBy(f => f.File == main ? 0 : 1) // main first, then subagents in the order they appeared
      .ThenBy(f => f.FirstSeenAt)
      .Select(f => new
      {
        f.Agent,
        f.File,
        f.SubagentId,
        f.Meta,
        f.FirstSeenAt,
        LineCount = db.TranscriptLines.Count(l => l.Agent == f.Agent && l.NativeSessionId == f.NativeSessionId && l.File == f.File),
      })
      .ToListAsync(cancellationToken);

    // In memory: unwrap value objects, which EF can't translate.
    return [.. rows.Select(r => new TranscriptFileItem(r.Agent.Name, r.File.Value, r.SubagentId, r.Meta, r.FirstSeenAt, r.LineCount))];
  }

  public async Task<TranscriptLinePage> ListLinesAsync(string nativeSessionId, TranscriptFileName file, TranscriptLineCursor? after, int limit,
    CancellationToken cancellationToken)
  {
    var lines = db.TranscriptLines.AsNoTracking()
      .Where(l => l.NativeSessionId == nativeSessionId && l.File == file);

    // Keyset paging on (byte offset, line ID): after a rewrite two lines can share an offset (ADR 0016, 0020).
    // Vogen IDs don't define '<'; CompareTo(...) > 0 is translated to a plain SQL comparison.
    if (after is not null)
    {
      var afterId = TranscriptLineId.From(after.LineId);
      lines = lines.Where(l => l.ByteOffset > after.ByteOffset || (l.ByteOffset == after.ByteOffset && l.Id.CompareTo(afterId) > 0));
    }

    var rows = await lines
      .OrderBy(l => l.ByteOffset)
      .ThenBy(l => l.Id)
      .Take(limit + 1) // one extra row says whether another page exists, without a COUNT
      .Select(l => new { l.Id, l.ByteOffset, l.Text, l.ReceivedAt })
      .ToListAsync(cancellationToken);

    var items = rows.Take(limit).Select(r => new TranscriptLineItem(r.Id.Value, r.ByteOffset, r.Text, r.ReceivedAt)).ToList();
    var next = rows.Count > limit ? new TranscriptLineCursor(items[^1].ByteOffset, items[^1].LineId) : null;
    return new TranscriptLinePage(items, next);
  }
}
