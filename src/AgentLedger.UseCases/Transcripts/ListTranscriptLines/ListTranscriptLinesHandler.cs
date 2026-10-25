using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UseCases.Transcripts.ListTranscriptLines;

// Transcript lines newest first, filtered and keyset-paged like raw events (ADR 0020, revised).
public sealed class ListTranscriptLinesHandler(ITranscriptQueryService queries)
  : IQueryHandler<ListTranscriptLinesQuery, Result<TranscriptLinePage>>
{
  public async ValueTask<Result<TranscriptLinePage>> Handle(ListTranscriptLinesQuery query, CancellationToken cancellationToken)
  {
    var filter = query.Filter;
    if (filter.Agent is not null && !AgentKind.TryFromName(filter.Agent, out _))
    {
      return Result.Invalid(new ValidationError("agent", $"Unknown agent: {filter.Agent}"));
    }

    if (filter.File is not null && !TranscriptFileName.TryFrom(filter.File, out _))
    {
      return Result.Invalid(new ValidationError("file", $"File must be 'main' or 'subagents/agent-<id>', but was '{filter.File}'."));
    }

    if (filter.From is { } from && filter.To is { } to && to <= from)
    {
      return Result.Invalid(new ValidationError("to", "'to' must be after 'from'."));
    }

    if (query.Limit is < 1 or > Constants.MAX_PAGE_SIZE)
    {
      return Result.Invalid(new ValidationError("limit", $"Limit must be between 1 and {Constants.MAX_PAGE_SIZE}."));
    }

    return Result.Success(await queries.ListLinesAsync(filter, query.After, query.Limit, cancellationToken));
  }
}
