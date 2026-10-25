namespace AgentLedger.UseCases.Transcripts.ListTranscriptLines;

public sealed record ListTranscriptLinesQuery(string NativeSessionId, string File, TranscriptLineCursor? After, int Limit)
  : IQuery<Result<TranscriptLinePage>>;
