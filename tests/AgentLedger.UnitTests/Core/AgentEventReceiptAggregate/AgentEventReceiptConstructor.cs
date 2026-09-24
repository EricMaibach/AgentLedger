using AgentLedger.Core.AgentEventReceiptAggregate;

namespace AgentLedger.UnitTests.Core.AgentEventReceiptAggregate;

public sealed class AgentEventReceiptConstructor
{
  [Fact]
  public void StoresEveryValue()
  {
    var receiptId = ReceiptId.From(Guid.CreateVersion7());
    var eventId = AgentEventId.From(Guid.CreateVersion7());
    var context = new CaptureContext("devbox", "eric", "/workspace", "github.com/EricMaibach/AgentLedger", "main", "/workspace");
    var tags = new Dictionary<string, string> { ["story"] = "123" };

    var receipt = new AgentEventReceipt(receiptId, eventId, AgentKind.Codex, "Stop", "session-1",
      AgentEventReceiptBuilder.CapturedAt, AgentEventReceiptBuilder.ReceivedAt, context, tags, AgentEventReceiptBuilder.Payload);

    receipt.Id.ShouldBe(receiptId);
    receipt.EventId.ShouldBe(eventId);
    receipt.Agent.ShouldBe(AgentKind.Codex);
    receipt.EventType.ShouldBe("Stop");
    receipt.NativeSessionId.ShouldBe("session-1");
    receipt.CapturedAt.ShouldBe(AgentEventReceiptBuilder.CapturedAt);
    receipt.ReceivedAt.ShouldBe(AgentEventReceiptBuilder.ReceivedAt);
    receipt.Context.ShouldBe(context);
    receipt.Tags.ShouldBe(tags);
    receipt.Payload.ShouldBe(AgentEventReceiptBuilder.Payload);
  }

  [Fact]
  public void KeepsPayloadExactlyAsReceived()
  {
    // The ledger stores raw data: whitespace, key order and unknown fields must survive untouched.
    const string payload = "{ \"z\": 1,\n  \"a\": [true, null],  \"unknown_field\": {} }";

    var receipt = new AgentEventReceiptBuilder().WithPayload(payload).Build();

    receipt.Payload.ShouldBe(payload);
  }

  [Fact]
  public void AllowsMissingNativeSessionId()
  {
    // VS Code Copilot's session id is optional.
    var receipt = new AgentEventReceiptBuilder().WithNativeSessionId(null).Build();

    receipt.NativeSessionId.ShouldBeNull();
  }

  [Fact]
  public void AllowsNoTags()
  {
    // Agents started by hand have no AGENTLEDGER_TAG_* variables.
    var receipt = new AgentEventReceiptBuilder().WithTags(new Dictionary<string, string>()).Build();

    receipt.Tags.ShouldBeEmpty();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void RejectsMissingEventType(string? eventType)
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventReceiptBuilder().WithEventType(eventType!).Build());
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void RejectsMissingPayload(string? payload)
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventReceiptBuilder().WithPayload(payload!).Build());
  }

  [Fact]
  public void RejectsNullAgent()
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventReceiptBuilder().WithAgent(null!).Build());
  }

  [Fact]
  public void RejectsNullContext()
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventReceiptBuilder().WithContext(null!).Build());
  }

  [Fact]
  public void RejectsNullTags()
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventReceiptBuilder().WithTags(null!).Build());
  }

  [Fact]
  public void RejectsMissingCapturedAt()
  {
    // A timestamp missing from the CLI envelope deserializes to default: 0001-01-01.
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventReceiptBuilder().WithCapturedAt(default).Build());
  }

  [Fact]
  public void RejectsMissingReceivedAt()
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventReceiptBuilder().WithReceivedAt(default).Build());
  }

  [Fact]
  public void TagsAreNotAffectedByLaterChangesToTheCallersDictionary()
  {
    var tags = new Dictionary<string, string> { ["story"] = "123" };
    var receipt = new AgentEventReceiptBuilder().WithTags(tags).Build();

    tags["story"] = "999";
    tags["extra"] = "added later";

    receipt.Tags["story"].ShouldBe("123");
    receipt.Tags.Count.ShouldBe(1);
  }

  [Fact]
  public void KeepsReceiptIdAndEventIdDistinct()
  {
    // A resent event is a new receipt of the same agent event: same EventId, different receipt Id.
    var eventId = AgentEventId.From(Guid.CreateVersion7());

    var first = new AgentEventReceiptBuilder().WithEventId(eventId).Build();
    var resend = new AgentEventReceiptBuilder().WithEventId(eventId).Build();

    resend.EventId.ShouldBe(first.EventId);
    resend.Id.ShouldNotBe(first.Id);
  }
}
