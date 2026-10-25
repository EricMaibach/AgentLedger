using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UnitTests.Core.TranscriptAggregate;

public sealed class TranscriptLineConstructor
{
  private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
  private static readonly ApiKeyId Key = ApiKeyId.From(Guid.CreateVersion7());

  private static TranscriptLine Line(string session = "s1", string file = "main", long offset = 0, string text = """{"type":"user"}""") =>
    new(AgentKind.ClaudeCode, session, TranscriptFileName.From(file), offset, text, Key, ReceivedAt);

  [Fact]
  public void StoresEveryValueAndComputesItsId()
  {
    var line = Line(file: "subagents/agent-a1b2c3", offset: 412);

    line.Id.ShouldBe(TranscriptLineId.For(AgentKind.ClaudeCode, "s1", TranscriptFileName.ForSubagent("a1b2c3"), 412, """{"type":"user"}"""));
    line.Agent.ShouldBe(AgentKind.ClaudeCode);
    line.NativeSessionId.ShouldBe("s1");
    line.File.ShouldBe(TranscriptFileName.ForSubagent("a1b2c3"));
    line.SubagentId.ShouldBe("a1b2c3");
    line.ByteOffset.ShouldBe(412);
    line.Text.ShouldBe("""{"type":"user"}""");
    line.ApiKeyId.ShouldBe(Key);
    line.ReceivedAt.ShouldBe(ReceivedAt);
  }

  [Fact]
  public void KeepsTheTextExactlyEvenIfItIsNotJson()
  {
    // A file we read: stored byte for byte, valid JSON or not (ADR 0020).
    Line(text: "{\"type\": \"user\",  not json").Text.ShouldBe("{\"type\": \"user\",  not json");
  }

  [Fact]
  public void AllowsAnEmptyLine()
  {
    Line(text: "").Text.ShouldBe("");
  }

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  public void RequiresASession(string session)
  {
    Assert.ThrowsAny<ArgumentException>(() => Line(session: session));
  }

  [Fact]
  public void RejectsANegativeOffset()
  {
    Assert.ThrowsAny<ArgumentException>(() => Line(offset: -1));
  }
}

public sealed class TranscriptFileConstructor
{
  private static readonly DateTimeOffset FirstSeen = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
  private static readonly ApiKeyId Key = ApiKeyId.From(Guid.CreateVersion7());

  [Fact]
  public void StoresASubagentFileWithItsMetaExactly()
  {
    const string meta = """{"agentType":"Explore", "toolUseId":"toolu_1","spawnDepth":1}""";

    var file = new TranscriptFile(AgentKind.ClaudeCode, "s1", TranscriptFileName.ForSubagent("a1b2c3"), meta, Key, FirstSeen);

    file.Id.ShouldBe(TranscriptFileId.For(AgentKind.ClaudeCode, "s1", TranscriptFileName.ForSubagent("a1b2c3")));
    file.SubagentId.ShouldBe("a1b2c3");
    file.Meta.ShouldBe(meta);
    file.ApiKeyId.ShouldBe(Key);
    file.FirstSeenAt.ShouldBe(FirstSeen);
  }

  [Fact]
  public void AllowsNoMeta()
  {
    new TranscriptFile(AgentKind.ClaudeCode, "s1", TranscriptFileName.Main, null, Key, FirstSeen).Meta.ShouldBeNull();
  }

  [Fact]
  public void RequiresASession()
  {
    Assert.ThrowsAny<ArgumentException>(() => new TranscriptFile(AgentKind.ClaudeCode, " ", TranscriptFileName.Main, null, Key, FirstSeen));
  }
}
