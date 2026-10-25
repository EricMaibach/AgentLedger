using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UseCases.Transcripts;

// Write store for transcript batches (docs/architecture.md, "Write store"; ADR 0020): one batch in one
// statement, skipping lines already stored, which the generic repository can't express.
public interface ITranscriptStore
{
  // Records the file (once; its meta is filled in the first time one arrives, never overwritten) and inserts the
  // lines that aren't stored yet. Returns how many lines were new.
  Task<int> AddAsync(TranscriptFile file, IReadOnlyList<TranscriptLine> lines, CancellationToken cancellationToken);
}
