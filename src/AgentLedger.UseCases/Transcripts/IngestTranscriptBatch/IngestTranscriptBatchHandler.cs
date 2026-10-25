using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UseCases.Transcripts.IngestTranscriptBatch;

// Stores one chunk of one transcript file (ADR 0020). The envelope can be refused; a line's content never is.
public sealed class IngestTranscriptBatchHandler(ITranscriptStore store, TimeProvider clock)
  : ICommandHandler<IngestTranscriptBatchCommand, Result<TranscriptBatchStored>>
{
  public async ValueTask<Result<TranscriptBatchStored>> Handle(IngestTranscriptBatchCommand command, CancellationToken cancellationToken)
  {
    if (!AgentKind.TryFromName(command.Agent, out var agent))
    {
      return Invalid("agent", $"Unknown agent: {command.Agent}");
    }

    if (string.IsNullOrWhiteSpace(command.NativeSessionId))
    {
      return Invalid("nativeSessionId", "A session ID is required.");
    }

    if (!TranscriptFileName.TryFrom(command.File, out var file))
    {
      return Invalid("file", $"File must be 'main' or 'subagents/agent-<id>', but was '{command.File}'.");
    }

    if (command.SubagentMeta is not null && file.SubagentId is null)
    {
      return Invalid("subagentMeta", "Only a subagent's file has meta.");
    }

    if (command.Lines.Count is 0 or > Constants.MAX_TRANSCRIPT_BATCH_LINES)
    {
      return Invalid("lines", $"A batch has 1 to {Constants.MAX_TRANSCRIPT_BATCH_LINES} lines, but had {command.Lines.Count}.");
    }

    if (command.Lines.Any(l => l.ByteOffset < 0 || l.Text is null)
        || command.Lines.Select(l => l.ByteOffset).Distinct().Count() != command.Lines.Count)
    {
      return Invalid("lines", "Every line needs its text and its own byte offset (not negative).");
    }

    var now = clock.GetUtcNow();
    var keyId = ApiKeyId.From(command.ApiKeyId);
    var transcriptFile = new TranscriptFile(agent, command.NativeSessionId, file, command.SubagentMeta, keyId, now);
    var lines = command.Lines
      .Select(l => new TranscriptLine(agent, command.NativeSessionId, file, l.ByteOffset, l.Text, keyId, now))
      .ToList();

    var stored = await store.AddAsync(transcriptFile, lines, cancellationToken);
    return new TranscriptBatchStored(stored, lines.Count - stored);
  }

  private static Result<TranscriptBatchStored> Invalid(string field, string message) =>
    Result.Invalid(new ValidationError(field, message));
}
