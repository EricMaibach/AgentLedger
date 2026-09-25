using System.Text.Json;
using AgentLedger.Cli.Envelope;

namespace AgentLedger.Cli.Tests.Envelope;

public sealed class EnvelopeJsonWrite
{
  // Whitespace, key order and a duplicate key must survive untouched (ADR 0009, ADR 0011).
  private const string OddPayload = "{ \"z\": 1,\n  \"a\": [true, null],  \"a\": \"dup\" }";

  private static EventEnvelope NewEnvelope(string payload = OddPayload) => new(
    EventId: Guid.CreateVersion7(),
    Agent: "claude-code",
    EventType: "PreToolUse",
    NativeSessionId: "3829bce8",
    CapturedAt: new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero),
    Host: "devbox",
    User: "eric@example.com",
    ProjectDir: "/workspace",
    GitRepo: "github.com/owner/repo",
    GitBranch: "main",
    GitWorktree: null,
    Tags: new Dictionary<string, string> { ["story"] = "123" },
    Payload: payload);

  [Fact]
  public void WritesTheContractFieldsInCamelCase()
  {
    var envelope = NewEnvelope();

    using var json = JsonDocument.Parse(EnvelopeJson.Write(envelope));
    var root = json.RootElement;

    root.GetProperty("eventId").GetGuid().ShouldBe(envelope.EventId);
    root.GetProperty("agent").GetString().ShouldBe("claude-code");
    root.GetProperty("eventType").GetString().ShouldBe("PreToolUse");
    root.GetProperty("nativeSessionId").GetString().ShouldBe("3829bce8");
    root.GetProperty("capturedAt").GetDateTimeOffset().ShouldBe(envelope.CapturedAt);
    root.GetProperty("host").GetString().ShouldBe("devbox");
    root.GetProperty("user").GetString().ShouldBe("eric@example.com");
    root.GetProperty("projectDir").GetString().ShouldBe("/workspace");
    root.GetProperty("gitRepo").GetString().ShouldBe("github.com/owner/repo");
    root.GetProperty("gitBranch").GetString().ShouldBe("main");
    root.GetProperty("gitWorktree").ValueKind.ShouldBe(JsonValueKind.Null);
    root.GetProperty("tags").GetProperty("story").GetString().ShouldBe("123");
  }

  [Fact]
  public void EmbedsThePayloadAsRawJsonExactlyAsGiven()
  {
    var json = EnvelopeJson.Write(NewEnvelope(OddPayload));

    json.ShouldContain("\"payload\":" + OddPayload);
  }

  [Fact]
  public void EmbedsAPayloadThatIsNotJsonAsAString()
  {
    // Hook payloads are always JSON, but if one isn't, keep it rather than lose the event.
    var json = EnvelopeJson.Write(NewEnvelope("not json"));

    using var doc = JsonDocument.Parse(json);
    doc.RootElement.GetProperty("payload").GetString().ShouldBe("not json");
  }
}
