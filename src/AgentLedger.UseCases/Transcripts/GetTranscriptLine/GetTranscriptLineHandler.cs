using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.UseCases.ApiKeys;

namespace AgentLedger.UseCases.Transcripts.GetTranscriptLine;

// One transcript line, with who sent it (ADR 0019, 0020).
public sealed class GetTranscriptLineHandler(ITranscriptQueryService queries, IApiKeyQueryService apiKeys)
  : IQueryHandler<GetTranscriptLineQuery, Result<TranscriptLineDetail>>
{
  public async ValueTask<Result<TranscriptLineDetail>> Handle(GetTranscriptLineQuery query, CancellationToken cancellationToken)
  {
    if (!TranscriptLineId.TryFrom(query.LineId, out var lineId))
    {
      return Result.Invalid(new ValidationError("lineId", "LineId cannot be empty"));
    }

    if (await queries.GetLineAsync(lineId, cancellationToken) is not { } line)
    {
      return Result.NotFound();
    }

    var keyId = ApiKeyId.From(line.ApiKeyId);
    var senders = await apiKeys.GetSendersAsync([keyId], cancellationToken);
    return new TranscriptLineDetail(line, senders.GetValueOrDefault(keyId));
  }
}
