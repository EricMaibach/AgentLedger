namespace AgentLedger.Web.AgentEventReceipts;

public sealed class GetRawEventRequest
{
  // :guid: anything that isn't a GUID (e.g. /api/raw/events/not-a-guid) is simply not this route, so it gets a 404.
  // (Literal sibling routes like /api/raw/events/counts win over a parameter regardless.)
  public const string Route = "/api/raw/events/{EventId:guid}";
  public Guid EventId { get; set; }
}
