namespace AgentLedger.Core.AgentEventReceiptAggregate;

// One message received from an agent's hook, exactly as sent. A resent event is a new receipt
// with the same EventId; deduplication happens downstream (ADR 0010).
public sealed class AgentEventReceipt : EntityBase<AgentEventReceipt, ReceiptId>, IAggregateRoot
{
  public AgentEventId EventId { get; private set; }
  public AgentKind Agent { get; private set; }
  public string EventType { get; private set; }
  public string? NativeSessionId { get; private set; }
  public DateTimeOffset CapturedAt { get; private set; }
  public DateTimeOffset ReceivedAt { get; private set; }
  public CaptureContext Context { get; private set; }
  public IReadOnlyDictionary<string, string> Tags { get; private set; }
  public string Payload { get; private set; }

  public AgentEventReceipt(ReceiptId id, AgentEventId eventId, AgentKind agent, string eventType, string? nativeSessionId,
                           DateTimeOffset capturedAt, DateTimeOffset receivedAt, CaptureContext context,
                           IReadOnlyDictionary<string, string> tags, string payload)
  {
    Id = id;
    EventId = eventId;
    Agent = Guard.Against.Null(agent);
    EventType = Guard.Against.NullOrWhiteSpace(eventType);
    NativeSessionId = nativeSessionId;
    CapturedAt = Guard.Against.Default(capturedAt);
    ReceivedAt = Guard.Against.Default(receivedAt);
    Context = Guard.Against.Null(context);
    Tags = new Dictionary<string, string>(Guard.Against.Null(tags));
    Payload = Guard.Against.NullOrWhiteSpace(payload);
  }

#pragma warning disable CS8618 // Required by EF Core: properties are set when a row is loaded
  private AgentEventReceipt() { }
#pragma warning restore CS8618
}
