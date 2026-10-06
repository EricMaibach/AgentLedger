namespace AgentLedger.Web.AgentEventReceipts;

public sealed class GetRawEventRequest
{
  public const string Route = "/raw/events/{EventId}";
  public Guid EventId { get; set; }
}
