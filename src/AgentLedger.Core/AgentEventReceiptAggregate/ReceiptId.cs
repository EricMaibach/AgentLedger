using Vogen;

namespace AgentLedger.Core.AgentEventReceiptAggregate;

// Server-generated identity of one received message. Unlike AgentEventId, it differs between resends.
[ValueObject<Guid>]
public readonly partial struct ReceiptId
{
  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("ReceiptId cannot be empty.") : Validation.Ok;
}
