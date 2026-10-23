using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.AgentEventReceiptAggregate.Specifications;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.UseCases.ApiKeys;

namespace AgentLedger.UseCases.AgentEventReceipts.GetRawEvent;

public sealed class GetRawEventHandler(IReadRepository<AgentEventReceipt> repository, IApiKeyQueryService apiKeys) : IQueryHandler<GetRawEventQuery, Result<RawEventDetail>>
{
  public async ValueTask<Result<RawEventDetail>> Handle(GetRawEventQuery query, CancellationToken cancellationToken)
  {
    if (!AgentEventId.TryFrom(query.EventId, out var eventId))
    {
      return Result.Invalid(new ValidationError("eventId", "EventId cannot be empty"));
    }

    var receipts = await repository.ListAsync(new ReceiptsByEventIdSpec(eventId), cancellationToken);

    if (receipts.Count == 0)
    {
      return Result.NotFound();
    }

    var keyIds = receipts.Select(r => r.ApiKeyId).OfType<ApiKeyId>().Distinct().ToList();
    var senders = keyIds.Count == 0
      ? new Dictionary<ApiKeyId, ApiKeySender>()
      : await apiKeys.GetSendersAsync(keyIds, cancellationToken);

    return Result.Success(RawEventDetail.From(receipts, senders));
  }
}
