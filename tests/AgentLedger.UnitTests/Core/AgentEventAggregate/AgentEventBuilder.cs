using AgentLedger.Core.AgentEventAggregate;

namespace AgentLedger.UnitTests.Core.AgentEventAggregate;

// Test Data Builder: every test starts from a valid event and changes only the value it cares about,
// so each test reads as "a valid event, except…".
public sealed class AgentEventBuilder
{
  public static readonly DateTimeOffset CapturedAt = new(2026, 9, 23, 1, 0, 0, TimeSpan.Zero);
  public static readonly DateTimeOffset ReceivedAt = CapturedAt.AddMilliseconds(40);
  public const string Payload = """{"session_id":"abc","hook_event_name":"PreToolUse"}""";

  private AgentEventId _id = AgentEventId.From(Guid.CreateVersion7());
  private AgentKind _agent = AgentKind.ClaudeCode;
  private string _eventType = "PreToolUse";
  private string? _nativeSessionId = "3829bce8-0000-0000-0000-000000000000";
  private CaptureContext _context = new("devbox", "eric", "/workspace", "github.com/EricMaibach/AgentLedger", "main", null);
  private IReadOnlyDictionary<string, string> _tags = new Dictionary<string, string> { ["story"] = "123" };
  private DateTimeOffset _capturedAt = CapturedAt;
  private DateTimeOffset _receivedAt = ReceivedAt;
  private string _payload = Payload;

  public AgentEventBuilder WithId(AgentEventId id) { _id = id; return this; }
  public AgentEventBuilder WithAgent(AgentKind agent) { _agent = agent; return this; }
  public AgentEventBuilder WithEventType(string eventType) { _eventType = eventType; return this; }
  public AgentEventBuilder WithNativeSessionId(string? sessionId) { _nativeSessionId = sessionId; return this; }
  public AgentEventBuilder WithCapturedAt(DateTimeOffset capturedAt) { _capturedAt = capturedAt; return this; }
  public AgentEventBuilder WithReceivedAt(DateTimeOffset receivedAt) { _receivedAt = receivedAt; return this; }
  public AgentEventBuilder WithContext(CaptureContext context) { _context = context; return this; }
  public AgentEventBuilder WithTags(IReadOnlyDictionary<string, string> tags) { _tags = tags; return this; }
  public AgentEventBuilder WithPayload(string payload) { _payload = payload; return this; }

  public AgentEvent Build() =>
    new(_id, _agent, _eventType, _nativeSessionId, _capturedAt, _receivedAt, _context, _tags, _payload);
}
