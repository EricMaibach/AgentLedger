using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UseCases.Transcripts;

// Reading transcript lines back, like raw events (ADR 0020, revised; ADR 0016: query services for read models).
public interface ITranscriptQueryService
{
  // Lines newest first (received time, then byte offset, then line ID), strictly after the cursor.
  Task<TranscriptLinePage> ListLinesAsync(TranscriptLineFilter filter, TranscriptLineCursor? after, int limit, CancellationToken cancellationToken);

  Task<TranscriptLineRow?> GetLineAsync(TranscriptLineId lineId, CancellationToken cancellationToken);
}

// Every filter is optional. Agent is the domain name (e.g. "ClaudeCode"). Received time: From inclusive, To exclusive.
public sealed record TranscriptLineFilter(
  string? Agent = null, string? LineType = null, string? NativeSessionId = null, string? File = null,
  DateTimeOffset? From = null, DateTimeOffset? To = null);

public sealed record TranscriptLineListItem(Guid LineId, string Agent, string? LineType, string NativeSessionId, string File,
  string? SubagentId, long ByteOffset, DateTimeOffset ReceivedAt, string Preview)
{
  public const int PreviewLength = 100; // as RawEventListItem
}

// A batch arrives at one instant, so the byte offset keeps a session's lines in file order; the line ID breaks
// the remaining ties (a rewritten file can have two lines at one offset).
public sealed record TranscriptLineCursor(DateTimeOffset ReceivedAt, long ByteOffset, Guid LineId);

public sealed record TranscriptLinePage(IReadOnlyList<TranscriptLineListItem> Items, TranscriptLineCursor? Next);

// One line as stored, with its file's meta (a subagent's, if any).
public sealed record TranscriptLineRow(Guid LineId, string Agent, string? LineType, string NativeSessionId, string File,
  string? SubagentId, long ByteOffset, string Text, DateTimeOffset ReceivedAt, Guid ApiKeyId, string? FileMeta);
