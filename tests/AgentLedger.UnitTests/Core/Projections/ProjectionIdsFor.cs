using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;

namespace AgentLedger.UnitTests.Core.Projections;

// Projected rows get IDs from the raw identifiers they come from (ADR 0022), so a rebuild produces the same IDs
// and links survive it. Stored IDs and links depend on these algorithms: they must never change.
public sealed class ProjectionIdsFor
{
  private const string Session = "3829bce8-470e-43d0-a99e-5495558d1976";
  private static readonly AgentKind Agent = AgentKind.ClaudeCode;

  [Fact]
  public void NeverChange()
  {
    // Computed independently (Python): SHA-256 of the length-prefixed fields, first 16 bytes, UUID version 8.
    SessionId.For(Agent, Session).Value.ShouldBe(Guid.Parse("71f4f7b8-0278-8bb4-96d2-36eef667eff5"));
    TurnId.For(Agent, Session, "p-1").Value.ShouldBe(Guid.Parse("e5a4c368-a8db-854e-80bd-21608ad12c76"));
    StepId.For(Agent, Session, StepKind.ToolCall, "toolu_01").Value.ShouldBe(Guid.Parse("b20e656a-dca0-8aaf-ba6f-651829f1bb2f"));
    ModelResponseId.For(Agent, Session, "msg_01").Value.ShouldBe(Guid.Parse("94d66c40-3007-81bd-bb5e-d6988f11ef54"));
  }

  [Fact]
  public void AreTheSameForTheSameSource()
  {
    TurnId.For(Agent, Session, "p-1").ShouldBe(TurnId.For(Agent, Session, "p-1"));
    StepId.For(Agent, Session, StepKind.Prompt, "p-1").ShouldBe(StepId.For(Agent, Session, StepKind.Prompt, "p-1"));
  }

  [Fact]
  public void DifferPerAgentSessionAndKey()
  {
    new[]
    {
      TurnId.For(Agent, Session, "p-1"),
      TurnId.For(Agent, Session, "p-2"),
      TurnId.For(Agent, "another-session", "p-1"),
      TurnId.For(AgentKind.Codex, Session, "p-1"),
    }.Distinct().Count().ShouldBe(4);
  }

  [Fact]
  public void StepsDifferPerKindWithTheSameKey()
  {
    // A prompt step and its turn share the prompt ID as their native key; they must still differ.
    StepId.For(Agent, Session, StepKind.Prompt, "k").ShouldNotBe(StepId.For(Agent, Session, StepKind.Response, "k"));
    StepId.For(Agent, Session, StepKind.Prompt, "p-1").Value.ShouldNotBe(TurnId.For(Agent, Session, "p-1").Value);
  }

  [Fact]
  public void KindsOfIdDifferForTheSameFields()
  {
    TurnId.For(Agent, Session, "x").Value.ShouldNotBe(ModelResponseId.For(Agent, Session, "x").Value);
  }

  [Fact]
  public void RejectMissingParts()
  {
    Assert.ThrowsAny<ArgumentException>(() => SessionId.For(Agent, " "));
    Assert.ThrowsAny<ArgumentException>(() => TurnId.For(Agent, Session, ""));
    Assert.ThrowsAny<ArgumentException>(() => StepId.For(Agent, Session, StepKind.ToolCall, ""));
    Assert.ThrowsAny<ArgumentException>(() => ModelResponseId.For(Agent, Session, " "));
  }
}
