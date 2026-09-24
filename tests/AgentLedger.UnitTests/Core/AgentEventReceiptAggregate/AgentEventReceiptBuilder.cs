using AgentLedger.Core.AgentEventReceiptAggregate;

namespace AgentLedger.UnitTests.Core.AgentEventReceiptAggregate;

// Test Data Builder: every test starts from a valid receipt and changes only the value it cares about.
public sealed class AgentEventReceiptBuilder
{
  public static readonly DateTimeOffset CapturedAt = new(2026, 9, 23, 1, 0, 0, TimeSpan.Zero);
  public static readonly DateTimeOffset ReceivedAt = CapturedAt.AddMilliseconds(40);
  public const string Payload = """{"session_id":"abc","hook_event_name":"PreToolUse"}""";

  private ReceiptId _receiptId = ReceiptId.From(Guid.CreateVersion7());
  private AgentEventId _eventId = AgentEventId.From(Guid.CreateVersion7());
  private AgentKind _agent = AgentKind.ClaudeCode;
  private string _eventType = "PreToolUse";
  private string? _nativeSessionId = "3829bce8-0000-0000-0000-000000000000";
  private DateTimeOffset _capturedAt = CapturedAt;
  private DateTimeOffset _receivedAt = ReceivedAt;
  private CaptureContext _context = new("devbox", "eric", "/workspace", "github.com/EricMaibach/AgentLedger", "main", null);
  private IReadOnlyDictionary<string, string> _tags = new Dictionary<string, string> { ["story"] = "123" };
  private string _payload = Payload;

  public AgentEventReceiptBuilder WithReceiptId(ReceiptId receiptId) { _receiptId = receiptId; return this; }
  public AgentEventReceiptBuilder WithEventId(AgentEventId eventId) { _eventId = eventId; return this; }
  public AgentEventReceiptBuilder WithAgent(AgentKind agent) { _agent = agent; return this; }
  public AgentEventReceiptBuilder WithEventType(string eventType) { _eventType = eventType; return this; }
  public AgentEventReceiptBuilder WithNativeSessionId(string? sessionId) { _nativeSessionId = sessionId; return this; }
  public AgentEventReceiptBuilder WithCapturedAt(DateTimeOffset capturedAt) { _capturedAt = capturedAt; return this; }
  public AgentEventReceiptBuilder WithReceivedAt(DateTimeOffset receivedAt) { _receivedAt = receivedAt; return this; }
  public AgentEventReceiptBuilder WithContext(CaptureContext context) { _context = context; return this; }
  public AgentEventReceiptBuilder WithTags(IReadOnlyDictionary<string, string> tags) { _tags = tags; return this; }
  public AgentEventReceiptBuilder WithPayload(string payload) { _payload = payload; return this; }

  public AgentEventReceipt Build() =>
    new(_receiptId, _eventId, _agent, _eventType, _nativeSessionId, _capturedAt, _receivedAt, _context, _tags, _payload);
}
