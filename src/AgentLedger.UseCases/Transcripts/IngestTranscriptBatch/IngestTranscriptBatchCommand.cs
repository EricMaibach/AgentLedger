namespace AgentLedger.UseCases.Transcripts.IngestTranscriptBatch;

// One chunk of one transcript file (ADR 0020). Agent is the domain name (e.g. "ClaudeCode"); ApiKeyId is the
// authenticated sender (ADR 0019).
public sealed record IngestTranscriptBatchCommand(
  string Agent, string NativeSessionId, string File, string? SubagentMeta, IReadOnlyList<TranscriptLineInput> Lines, Guid ApiKeyId)
  : ICommand<Result<TranscriptBatchStored>>;

public sealed record TranscriptLineInput(long ByteOffset, string Text);

public sealed record TranscriptBatchStored(int Stored, int AlreadyStored);
