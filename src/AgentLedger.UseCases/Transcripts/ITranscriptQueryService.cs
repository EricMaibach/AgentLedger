using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UseCases.Transcripts;

// Reading transcripts back (ADR 0020; ADR 0016: query services for read models).
public interface ITranscriptQueryService
{
  // A session's files: main first, then subagents in the order they appeared.
  Task<IReadOnlyList<TranscriptFileItem>> ListFilesAsync(string nativeSessionId, CancellationToken cancellationToken);

  // One file's lines in order, strictly after the cursor (keyset paging).
  Task<TranscriptLinePage> ListLinesAsync(string nativeSessionId, TranscriptFileName file, TranscriptLineCursor? after, int limit,
    CancellationToken cancellationToken);
}

public sealed record TranscriptFileItem(string Agent, string File, string? SubagentId, string? Meta, DateTimeOffset FirstSeenAt, int LineCount);

public sealed record TranscriptLineItem(Guid LineId, long ByteOffset, string Text, DateTimeOffset ReceivedAt);

// Offset and line ID: after a rewrite two different lines can share an offset, and paging must not skip one.
public sealed record TranscriptLineCursor(long ByteOffset, Guid LineId);

public sealed record TranscriptLinePage(IReadOnlyList<TranscriptLineItem> Items, TranscriptLineCursor? Next);
