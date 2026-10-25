using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UseCases.Transcripts.ListTranscriptLines;

public sealed class ListTranscriptLinesHandler(ITranscriptQueryService queries)
  : IQueryHandler<ListTranscriptLinesQuery, Result<TranscriptLinePage>>
{
  public async ValueTask<Result<TranscriptLinePage>> Handle(ListTranscriptLinesQuery query, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(query.NativeSessionId))
    {
      return Result.Invalid(new ValidationError("session", "A session ID is required."));
    }

    if (!TranscriptFileName.TryFrom(query.File, out var file))
    {
      return Result.Invalid(new ValidationError("file", $"File must be 'main' or 'subagents/agent-<id>', but was '{query.File}'."));
    }

    if (query.Limit is < 1 or > Constants.MAX_PAGE_SIZE)
    {
      return Result.Invalid(new ValidationError("limit", $"Limit must be between 1 and {Constants.MAX_PAGE_SIZE}."));
    }

    return Result.Success(await queries.ListLinesAsync(query.NativeSessionId, file, query.After, query.Limit, cancellationToken));
  }
}
