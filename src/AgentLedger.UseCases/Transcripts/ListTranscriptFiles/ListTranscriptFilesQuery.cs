namespace AgentLedger.UseCases.Transcripts.ListTranscriptFiles;

public sealed record ListTranscriptFilesQuery(string NativeSessionId) : IQuery<Result<IReadOnlyList<TranscriptFileItem>>>;
