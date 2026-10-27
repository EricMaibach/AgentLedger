using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UnitTests.Core.Projections;

public sealed class LineageConstructor
{
  private static readonly AgentEventId Event = AgentEventId.From(Guid.Parse("0192f0aa-0000-7000-8000-000000000001"));
  private static readonly TranscriptLineId Line = TranscriptLineId.From(Guid.Parse("15803c1c-b396-8d0e-864f-444c9cdc2157"));

  [Fact]
  public void RequiresEvidence()
  {
    // Every projected row is built from something: lineage without sources would be a broken link to the evidence.
    Assert.ThrowsAny<ArgumentException>(() => new Lineage([], []));
  }

  [Fact]
  public void DropsRepeatsAndCopiesItsInput()
  {
    var events = new List<AgentEventId> { Event, Event };
    var lineage = new Lineage(events, [Line, Line]);
    events.Clear();

    lineage.EventIds.ShouldBe([Event]);
    lineage.LineIds.ShouldBe([Line]);
  }

  [Fact]
  public void CombinesSources()
  {
    var combined = Lineage.Combine([new Lineage([Event], []), new Lineage([Event], [Line])]);

    combined.EventIds.ShouldBe([Event]);
    combined.LineIds.ShouldBe([Line]);
  }
}

public sealed class TokenUsageConstructor
{
  [Fact]
  public void KeepsEachKind()
  {
    var usage = new TokenUsage(Input: 2, Output: 836, CacheWrite5m: 0, CacheWrite1h: 56, CacheRead: 746_742, Thinking: 81,
      WebSearches: 1, WebFetches: 2);

    (usage.Input, usage.Output, usage.CacheWrite1h, usage.CacheRead, usage.Thinking).ShouldBe((2L, 836L, 56L, 746_742L, 81L));
  }

  [Theory]
  [InlineData(-1, 0, 0, 0, 0, 0, 0, 0)]
  [InlineData(0, -1, 0, 0, 0, 0, 0, 0)]
  [InlineData(0, 0, -1, 0, 0, 0, 0, 0)]
  [InlineData(0, 0, 0, -1, 0, 0, 0, 0)]
  [InlineData(0, 0, 0, 0, -1, 0, 0, 0)]
  [InlineData(0, 0, 0, 0, 0, -1, 0, 0)]
  [InlineData(0, 0, 0, 0, 0, 0, -1, 0)]
  [InlineData(0, 0, 0, 0, 0, 0, 0, -1)]
  public void RejectsNegativeCounts(long input, long output, long w5, long w1, long read, long thinking, long searches, long fetches)
  {
    Assert.ThrowsAny<ArgumentException>(() => new TokenUsage(input, output, w5, w1, read, thinking, searches, fetches));
  }
}

public sealed class StepFactories
{
  [Fact]
  public void PromptHasItsText()
  {
    var step = Projected.Prompt();

    step.Kind.ShouldBe(StepKind.Prompt);
    step.Text.ShouldBe("Add a test");
  }

  [Fact]
  public void ThinkingWithoutTextIsAllowed()
  {
    // Most thinking blocks hold only a signature (ADR 0022): present, with no text to show.
    var step = Step.Thinking(Projected.StepId(StepKind.Thinking, "l#0"), Projected.T0, "", Projected.ResponseId(), Projected.Lineage());

    step.Kind.ShouldBe(StepKind.Thinking);
    step.Text.ShouldBeNull();
    step.ResponseId.ShouldBe(Projected.ResponseId());
  }

  [Theory]
  [InlineData("Fix the cart", "Fix the cart")]
  [InlineData("\n\n  Fix the cart  \nthen run the tests", "Fix the cart")]
  [InlineData("# Plan\r\nStep one", "# Plan")]
  public void TextStepsAreSummarizedByTheirFirstLine(string text, string summary)
  {
    Step.Prompt(Projected.StepId(StepKind.Prompt, "p"), Projected.T0, text, Projected.Lineage()).Summary.ShouldBe(summary);
    Step.Response(Projected.StepId(StepKind.Response, "r"), Projected.T0, text, null, Projected.Lineage()).Summary.ShouldBe(summary);
    Step.Thinking(Projected.StepId(StepKind.Thinking, "t"), Projected.T0, text, null, Projected.Lineage()).Summary.ShouldBe(summary);
  }

  [Fact]
  public void ThinkingWithoutTextHasNoSummary()
  {
    Step.Thinking(Projected.StepId(StepKind.Thinking, "t"), Projected.T0, null, null, Projected.Lineage()).Summary.ShouldBeNull();
  }

  [Fact]
  public void SummariesAreShortened()
  {
    var summary = Step.Prompt(Projected.StepId(StepKind.Prompt, "p"), Projected.T0, new string('x', 500), Projected.Lineage()).Summary;

    summary!.Length.ShouldBe(Step.MaxSummaryLength);
    summary.ShouldEndWith("…");
  }

  [Fact]
  public void ToolCallSummaryComesFromTheAdapter()
  {
    // What sums up a tool call depends on the tool, which only the agent's adapter knows (ADR 0003).
    var step = Step.ToolCall(Projected.StepId(StepKind.ToolCall, "toolu_01"), Projected.T0, "Bash", "toolu_01", null, null, null, null,
      null, Projected.Lineage(), summary: "docker ps\n--all");

    step.Summary.ShouldBe("docker ps");
  }

  [Fact]
  public void ResponseNeedsText()
  {
    Assert.ThrowsAny<ArgumentException>(() =>
      Step.Response(Projected.StepId(StepKind.Response, "l#0"), Projected.T0, " ", null, Projected.Lineage()));
  }

  [Fact]
  public void ToolCallHasItsToolAndOutcome()
  {
    var step = Step.ToolCall(Projected.StepId(StepKind.ToolCall, "toolu_01"), Projected.T0, "Bash", "toolu_01",
      """{"command":"ls"}""", "a.txt", succeeded: true, endedAt: Projected.T0.AddSeconds(3), Projected.ResponseId(),
      Projected.Lineage());

    (step.Kind, step.ToolName, step.ToolUseId, step.Succeeded).ShouldBe((StepKind.ToolCall, "Bash", "toolu_01", (bool?)true));
    step.EndedAt.ShouldBe(Projected.T0.AddSeconds(3));
  }

  [Fact]
  public void ToolCallWithoutResultIsAllowed()
  {
    // An interrupted or still-running call has no result yet.
    var step = Step.ToolCall(Projected.StepId(StepKind.ToolCall, "toolu_01"), Projected.T0, "Bash", "toolu_01", null, null,
      succeeded: null, endedAt: null, null, Projected.Lineage());

    (step.ToolOutput, step.Succeeded, step.EndedAt).ShouldBe((null, null, null));
  }

  [Fact]
  public void ToolCallNeedsItsToolAndId()
  {
    var id = Projected.StepId(StepKind.ToolCall, "toolu_01");
    Assert.ThrowsAny<ArgumentException>(() =>
      Step.ToolCall(id, Projected.T0, "", "toolu_01", null, null, null, null, null, Projected.Lineage()));
    Assert.ThrowsAny<ArgumentException>(() =>
      Step.ToolCall(id, Projected.T0, "Bash", " ", null, null, null, null, null, Projected.Lineage()));
  }

  [Fact]
  public void CannotEndBeforeItStarts()
  {
    Assert.ThrowsAny<ArgumentException>(() =>
      Step.ToolCall(Projected.StepId(StepKind.ToolCall, "toolu_01"), Projected.T0, "Bash", "toolu_01", null, "x", true,
        Projected.T0.AddSeconds(-1), null, Projected.Lineage()));
  }
}

public sealed class TurnConstructor
{
  [Fact]
  public void KeepsStepsInOrderAndCopiesThem()
  {
    var response = Step.Response(Projected.StepId(StepKind.Response, "l#0"), Projected.T0.AddSeconds(2), "Done",
      Projected.ResponseId(), Projected.Lineage(2));
    var turn = Projected.Turn().WithSteps(Projected.Prompt(), response).WithResponses(Projected.Response()).Build();

    turn.Steps.Select(s => s.Kind).ShouldBe([StepKind.Prompt, StepKind.Response]);
  }

  [Fact]
  public void LineageIsEverySourceOfItsStepsAndResponses()
  {
    var response = Step.Response(Projected.StepId(StepKind.Response, "l#0"), Projected.T0, "Done", Projected.ResponseId(),
      Projected.Lineage(2));
    var turn = Projected.Turn().WithSteps(Projected.Prompt(), response).WithResponses(Projected.Response()).Build();

    turn.Lineage.EventIds.Count.ShouldBe(3);
  }

  [Fact]
  public void LineageIncludesTheEvidenceOfHowItEnded()
  {
    // A Stop event decides the status without being part of any step.
    var turn = Projected.Turn().WithEnding(Projected.Lineage(9)).Build();

    turn.Lineage.EventIds.ShouldContain(Projected.Lineage(9).EventIds[0]);
  }

  [Fact]
  public void NeedsAtLeastOneStep()
  {
    Assert.ThrowsAny<ArgumentException>(() => Projected.Turn().WithSteps().Build());
  }

  [Fact]
  public void StepsReferToTheTurnsOwnResponses()
  {
    var orphan = Step.Response(Projected.StepId(StepKind.Response, "l#0"), Projected.T0, "Done",
      Projected.ResponseId("msg_other"), Projected.Lineage(2));

    Assert.ThrowsAny<ArgumentException>(() => Projected.Turn().WithSteps(Projected.Prompt(), orphan).Build());
  }

  [Fact]
  public void StepIdsAreUnique()
  {
    Assert.ThrowsAny<ArgumentException>(() => Projected.Turn().WithSteps(Projected.Prompt(), Projected.Prompt()).Build());
  }

  [Fact]
  public void InProgressMeansNotEnded()
  {
    Projected.Turn().InProgress().Build().EndedAt.ShouldBeNull();
    Assert.ThrowsAny<ArgumentException>(() => Projected.Turn().WithStatus(TurnStatus.InProgress, Projected.T0).Build());
    Assert.ThrowsAny<ArgumentException>(() => Projected.Turn().WithStatus(TurnStatus.Completed, null).Build());
  }

  [Fact]
  public void CannotEndBeforeItStarts()
  {
    Assert.ThrowsAny<ArgumentException>(() =>
      Projected.Turn().WithStatus(TurnStatus.Completed, Projected.T0.AddSeconds(-1)).Build());
  }

  [Fact]
  public void SumsTokensOfItsResponses()
  {
    var turn = Projected.Turn().WithResponses(Projected.Response("msg_01"), Projected.Response("msg_02")).Build();

    turn.Responses.Count.ShouldBe(2);
    turn.Responses.Sum(r => r.Usage.Output).ShouldBe(240);
  }
}

public sealed class ProjectedSessionConstructor
{
  [Fact]
  public void HasItsTurns()
  {
    var session = new ProjectedSession(AgentKind.ClaudeCode, Projected.Session, Projected.T0, Projected.T0.AddMinutes(5),
      [Projected.Turn("p-1").Build(), Projected.Turn("p-2").Build()]);

    session.Id.ShouldBe(SessionId.For(AgentKind.ClaudeCode, Projected.Session));
    session.Turns.Count.ShouldBe(2);
  }

  [Fact]
  public void MayHaveATitle()
  {
    new ProjectedSession(AgentKind.ClaudeCode, Projected.Session, Projected.T0, Projected.T0, [], "  Fix the cart  ").Title
      .ShouldBe("Fix the cart");
    new ProjectedSession(AgentKind.ClaudeCode, Projected.Session, Projected.T0, Projected.T0, [], " ").Title.ShouldBeNull();
  }

  [Fact]
  public void MayHaveNoTurns()
  {
    // A session that started and was closed before any prompt.
    new ProjectedSession(AgentKind.ClaudeCode, Projected.Session, Projected.T0, Projected.T0, []).Turns.ShouldBeEmpty();
  }

  [Fact]
  public void TurnsAreUnique()
  {
    Assert.ThrowsAny<ArgumentException>(() => new ProjectedSession(AgentKind.ClaudeCode, Projected.Session, Projected.T0,
      Projected.T0, [Projected.Turn("p-1").Build(), Projected.Turn("p-1").Build()]));
  }

  [Fact]
  public void LastActivityIsNotBeforeTheStart()
  {
    Assert.ThrowsAny<ArgumentException>(() => new ProjectedSession(AgentKind.ClaudeCode, Projected.Session, Projected.T0,
      Projected.T0.AddSeconds(-1), []));
  }
}
