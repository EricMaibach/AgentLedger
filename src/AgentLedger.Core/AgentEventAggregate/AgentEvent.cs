namespace AgentLedger.Core.AgentEventAggregate;

public sealed class AgentEvent : EntityBase<AgentEvent, AgentEventId>, IAggregateRoot
{
  public AgentKind Agent { get; private set; }
  public string EventType { get; private set; }
  public string? NativeSessionId { get; private set; }
  public DateTimeOffset CapturedAt { get; private set; }
  public DateTimeOffset ReceivedAt { get; private set; }
  public CaptureContext Context { get; private set; }
  public IReadOnlyDictionary<string, string> Tags { get; private set; }
  public string Payload { get; private set; }

  public AgentEvent(AgentEventId id, AgentKind agent, string eventType, string? nativeSessionId,
                    DateTimeOffset capturedAt, DateTimeOffset receivedAt, CaptureContext context,
                    IReadOnlyDictionary<string, string> tags, string payload)
  {
    Id = id;
    Agent = Guard.Against.Null(agent);
    EventType = Guard.Against.NullOrWhiteSpace(eventType);
    NativeSessionId = nativeSessionId;
    CapturedAt = Guard.Against.Default(capturedAt);
    ReceivedAt = Guard.Against.Default(receivedAt);
    Context = Guard.Against.Null(context);
    Tags = new Dictionary<string, string>(Guard.Against.Null(tags));
    Payload = Guard.Against.NullOrWhiteSpace(payload);

  }

#pragma warning disable CS8618
  private AgentEvent() { }
#pragma warning restore CS8618

}
