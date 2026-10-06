using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.AgentEventReceiptAggregate.Specifications;

namespace AgentLedger.UseCases.AgentEventReceipts.GetRawEvent;

public sealed class GetRawEventHandler(IReadRepository<AgentEventReceipt> repository) : IQueryHandler<GetRawEventQuery, Result<RawEventDetail>>
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

    return Result.Success(RawEventDetail.From(receipts));
  }
}
