using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UnitTests.Core.TranscriptAggregate;

// IDs computed from content (ADR 0020): anyone reading the same line gets the same ID, so a repeat is
// recognized. Stored IDs depend on this algorithm: it must never change.
public sealed class TranscriptLineIdFor
{
  private const string Session = "3829bce8-470e-43d0-a99e-5495558d1976";

  private static TranscriptLineId Id(string session = Session, string file = "main", long offset = 412,
    string text = """{"type":"user"}""", AgentKind? agent = null) =>
    TranscriptLineId.For(agent ?? AgentKind.ClaudeCode, session, TranscriptFileName.From(file), offset, text);

  [Fact]
  public void IsTheSameForTheSameLine()
  {
    Id().ShouldBe(Id());
  }

  [Fact]
  public void NeverChanges()
  {
    // Computed independently (Python): SHA-256 of the length-prefixed fields, first 16 bytes, UUID version 8.
    Id().Value.ShouldBe(Guid.Parse("15803c1c-b396-8d0e-864f-444c9cdc2157"));
  }

  [Fact]
  public void DiffersWhenAnyPartDiffers()
  {
    new[]
    {
      Id(),
      Id(session: "another-session"),
      Id(file: "subagents/agent-a1b2c3"),
      Id(offset: 413),
      Id(text: """{"type":"user" }"""),
      Id(agent: AgentKind.Codex),
    }.Distinct().Count().ShouldBe(6);
  }

  [Fact]
  public void CannotBeConfusedByWhereFieldsSplit()
  {
    // Length prefixes: "ab" + "c" and "a" + "bc" are different fields, so different IDs.
    TranscriptLineId.For(AgentKind.ClaudeCode, "s|1", TranscriptFileName.Main, 0, "x")
      .ShouldNotBe(TranscriptLineId.For(AgentKind.ClaudeCode, "s", TranscriptFileName.Main, 0, "x"));
  }
}

public sealed class TranscriptFileIdFor
{
  [Fact]
  public void NeverChanges()
  {
    TranscriptFileId.For(AgentKind.ClaudeCode, "3829bce8-470e-43d0-a99e-5495558d1976", TranscriptFileName.ForSubagent("a1b2c3"))
      .Value.ShouldBe(Guid.Parse("1d0a9b87-e28d-857a-9b38-bb1fead66201"));
  }

  [Fact]
  public void DiffersPerFile()
  {
    TranscriptFileId.For(AgentKind.ClaudeCode, "s", TranscriptFileName.Main)
      .ShouldNotBe(TranscriptFileId.For(AgentKind.ClaudeCode, "s", TranscriptFileName.ForSubagent("a1")));
  }
}
