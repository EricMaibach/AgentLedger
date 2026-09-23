using AgentLedger.Core.AgentEventAggregate;

namespace AgentLedger.UnitTests.Core.AgentEventAggregate;

public sealed class AgentEventConstructor
{
  [Fact]
  public void StoresEveryValue()
  {
    var id = AgentEventId.From(Guid.CreateVersion7());
    var context = new CaptureContext("devbox", "eric", "/workspace", "github.com/EricMaibach/AgentLedger", "main", "/workspace");
    var tags = new Dictionary<string, string> { ["story"] = "123" };

    var agentEvent = new AgentEvent(id, AgentKind.Codex, "Stop", "session-1",
      AgentEventBuilder.CapturedAt, AgentEventBuilder.ReceivedAt, context, tags, AgentEventBuilder.Payload);

    agentEvent.Id.ShouldBe(id);
    agentEvent.Agent.ShouldBe(AgentKind.Codex);
    agentEvent.EventType.ShouldBe("Stop");
    agentEvent.NativeSessionId.ShouldBe("session-1");
    agentEvent.CapturedAt.ShouldBe(AgentEventBuilder.CapturedAt);
    agentEvent.ReceivedAt.ShouldBe(AgentEventBuilder.ReceivedAt);
    agentEvent.Context.ShouldBe(context);
    agentEvent.Tags.ShouldBe(tags);
    agentEvent.Payload.ShouldBe(AgentEventBuilder.Payload);
  }

  [Fact]
  public void KeepsPayloadExactlyAsReceived()
  {
    // The ledger stores raw data: whitespace, key order and unknown fields must survive untouched.
    const string payload = "{ \"z\": 1,\n  \"a\": [true, null],  \"unknown_field\": {} }";

    var agentEvent = new AgentEventBuilder().WithPayload(payload).Build();

    agentEvent.Payload.ShouldBe(payload);
  }

  [Fact]
  public void AllowsMissingNativeSessionId()
  {
    // VS Code Copilot's session id is optional.
    var agentEvent = new AgentEventBuilder().WithNativeSessionId(null).Build();

    agentEvent.NativeSessionId.ShouldBeNull();
  }

  [Fact]
  public void AllowsNoTags()
  {
    // Agents started by hand have no AGENTLEDGER_TAG_* variables.
    var agentEvent = new AgentEventBuilder().WithTags(new Dictionary<string, string>()).Build();

    agentEvent.Tags.ShouldBeEmpty();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void RejectsMissingEventType(string? eventType)
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventBuilder().WithEventType(eventType!).Build());
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void RejectsMissingPayload(string? payload)
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventBuilder().WithPayload(payload!).Build());
  }

  [Fact]
  public void RejectsNullAgent()
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventBuilder().WithAgent(null!).Build());
  }

  [Fact]
  public void RejectsNullContext()
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventBuilder().WithContext(null!).Build());
  }

  [Fact]
  public void RejectsNullTags()
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventBuilder().WithTags(null!).Build());
  }

  [Fact]
  public void RejectsMissingCapturedAt()
  {
    // A timestamp missing from the CLI envelope deserializes to default: 0001-01-01.
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventBuilder().WithCapturedAt(default).Build());
  }

  [Fact]
  public void RejectsMissingReceivedAt()
  {
    Assert.ThrowsAny<ArgumentException>(() => new AgentEventBuilder().WithReceivedAt(default).Build());
  }

  [Fact]
  public void TagsAreNotAffectedByLaterChangesToTheCallersDictionary()
  {
    var tags = new Dictionary<string, string> { ["story"] = "123" };
    var agentEvent = new AgentEventBuilder().WithTags(tags).Build();

    tags["story"] = "999";
    tags["extra"] = "added later";

    agentEvent.Tags["story"].ShouldBe("123");
    agentEvent.Tags.Count.ShouldBe(1);
  }
}
