using AgentLedger.Core.AgentEventReceiptAggregate;

namespace AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

// Rules shared by every query that takes a RawEventFilter. Identifiers are the request fields.
internal static class RawEventFilterValidator
{
  public static List<ValidationError> Validate(RawEventFilter filter)
  {
    var errors = new List<ValidationError>();

    if (filter.Agent is not null && !AgentKind.TryFromName(filter.Agent, out _))
    {
      errors.Add(new ValidationError("agent", $"Unknown agent '{filter.Agent}'."));
    }

    if (filter.From is { } from && filter.To is { } to && to <= from)
    {
      errors.Add(new ValidationError("to", "The end of the time range must be after its start."));
    }

    return errors;
  }
}
