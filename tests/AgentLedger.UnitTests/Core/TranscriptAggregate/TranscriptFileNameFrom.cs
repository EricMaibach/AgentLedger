using AgentLedger.Core.TranscriptAggregate;
using Vogen;

namespace AgentLedger.UnitTests.Core.TranscriptAggregate;

// A file within a session, named relative to the session, not by machine path (ADR 0020).
public sealed class TranscriptFileNameFrom
{
  [Fact]
  public void AcceptsTheMainTranscript()
  {
    var main = TranscriptFileName.From("main");

    main.ShouldBe(TranscriptFileName.Main);
    main.SubagentId.ShouldBeNull();
  }

  [Theory]
  [InlineData("subagents/agent-a1b2c3", "a1b2c3")]
  [InlineData("subagents/agent-a63f_9-B", "a63f_9-B")]
  public void AcceptsASubagentAndKnowsItsId(string name, string subagentId)
  {
    TranscriptFileName.From(name).SubagentId.ShouldBe(subagentId);
  }

  [Fact]
  public void BuildsASubagentsName()
  {
    TranscriptFileName.ForSubagent("a1b2c3").ShouldBe(TranscriptFileName.From("subagents/agent-a1b2c3"));
  }

  [Theory]
  [InlineData("")]
  [InlineData("Main")]
  [InlineData("subagents/agent-")]
  [InlineData("subagents/a1b2c3")]
  [InlineData("subagents/agent-a1/../../etc")]
  [InlineData("/home/vscode/.claude/projects/-workspace/x.jsonl")]
  public void RejectsAnythingElse(string name)
  {
    Should.Throw<ValueObjectValidationException>(() => TranscriptFileName.From(name));
  }

  [Fact]
  public void LimitsTheSubagentIdLength()
  {
    TranscriptFileName.ForSubagent(new string('a', 64)).SubagentId!.Length.ShouldBe(64);
    Should.Throw<ValueObjectValidationException>(() => TranscriptFileName.ForSubagent(new string('a', 65)));
  }
}
