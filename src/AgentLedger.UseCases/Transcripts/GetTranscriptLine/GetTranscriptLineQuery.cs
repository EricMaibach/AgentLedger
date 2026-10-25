using AgentLedger.UseCases.ApiKeys;

namespace AgentLedger.UseCases.Transcripts.GetTranscriptLine;

public sealed record GetTranscriptLineQuery(Guid LineId) : IQuery<Result<TranscriptLineDetail>>;

// SentWith: the key that delivered the line and its owner (ADR 0019).
public sealed record TranscriptLineDetail(TranscriptLineRow Line, ApiKeySender? SentWith);
