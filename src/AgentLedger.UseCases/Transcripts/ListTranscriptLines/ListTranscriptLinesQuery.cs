namespace AgentLedger.UseCases.Transcripts.ListTranscriptLines;

public sealed record ListTranscriptLinesQuery(TranscriptLineFilter Filter, TranscriptLineCursor? After, int Limit)
  : IQuery<Result<TranscriptLinePage>>;
