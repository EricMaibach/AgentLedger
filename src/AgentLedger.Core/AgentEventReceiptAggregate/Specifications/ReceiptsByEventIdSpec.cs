namespace AgentLedger.Core.AgentEventReceiptAggregate.Specifications;

public sealed class ReceiptsByEventIdSpec : Specification<AgentEventReceipt>
{
  public ReceiptsByEventIdSpec(AgentEventId eventId)
  {
    Query
      .Where(receipt => receipt.EventId == eventId)
      .OrderBy(receipt => receipt.ReceivedAt)
      .ThenBy(receipt => receipt.Id);
  }
}
