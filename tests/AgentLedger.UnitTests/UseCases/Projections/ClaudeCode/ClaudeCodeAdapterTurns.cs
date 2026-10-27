using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using static AgentLedger.UnitTests.UseCases.Projections.ClaudeCode.ClaudeCodeSession;

namespace AgentLedger.UnitTests.UseCases.Projections.ClaudeCode;

// How the Claude Code adapter finds turns and their status (ADR 0022).
public sealed class ClaudeCodeAdapterTurns
{
  [Fact]
  public void OneTurnPerPromptIdInOrder()
  {
    var session = new ClaudeCodeSession()
      .PromptLine("p-1", "First", 0).Text("msg_1", 2, "One.")
      .PromptLine("p-2", "Second", 10).Text("msg_2", 12, "Two.")
      .Project();

    session.Turns.Select(t => t.PromptId).ShouldBe(["p-1", "p-2"]);
    session.Turns[0].Id.ShouldBe(TurnId.For(AgentKind.ClaudeCode, Id, "p-1"));
  }

  [Fact]
  public void FromTheTranscriptAlone()
  {
    // Prompts from before the hooks captured a session have no events (113 in the stable ledger, 2026-10-26).
    var turn = new ClaudeCodeSession().PromptLine("p-1", "Fix the cart", 0).Text("msg_1", 3, "Fixed.").Project().Turns.Single();

    turn.Steps[0].Kind.ShouldBe(StepKind.Prompt);
    turn.Steps[0].Text.ShouldBe("Fix the cart");
    turn.StartedAt.ShouldBe(At(0));
  }

  [Fact]
  public void FromTheHooksAlone()
  {
    var turn = new ClaudeCodeSession().UserPromptSubmit("p-1", "Fix the cart", 0).Stop("p-1", 9).Project().Turns.Single();

    turn.Steps.Single().Text.ShouldBe("Fix the cart");
    (turn.Status, turn.StartedAt, turn.EndedAt).ShouldBe((TurnStatus.Completed, At(0), (DateTimeOffset?)At(9)));
  }

  [Fact]
  public void HooksAndTranscriptMakeOnePromptStep()
  {
    var s = new ClaudeCodeSession().UserPromptSubmit("p-1", "Fix the cart", 0).PromptLine("p-1", "Fix the cart", 0.2);
    var turn = s.Project().Turns.Single();

    var prompt = turn.Steps.Single();
    prompt.Id.ShouldBe(StepIdFor(StepKind.Prompt, "p-1"));
    prompt.Lineage.EventIds.ShouldBe([s.Receipts[0].EventId]);
    prompt.Lineage.LineIds.ShouldBe([s.Lines[0].Id]);
  }

  [Fact]
  public void StopMeansCompleted()
  {
    var turn = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).Text("msg_1", 2, "Done.").Stop("p-1", 3).Project().Turns.Single();

    (turn.Status, turn.EndedAt).ShouldBe((TurnStatus.Completed, (DateTimeOffset?)At(3)));
  }

  [Fact]
  public void StopIsEvidenceOfTheTurn()
  {
    var s = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).Text("msg_1", 2, "Done.").Stop("p-1", 3);

    s.Project().Turns.Single().Lineage.EventIds.ShouldBe([s.Receipts[0].EventId]);
  }

  [Fact]
  public void StopFailureMeansFailed()
  {
    var turn = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).StopFailure("p-1", 4).Project().Turns.Single();

    (turn.Status, turn.EndedAt).ShouldBe((TurnStatus.Failed, (DateTimeOffset?)At(4)));
  }

  [Fact]
  public void FinalAnswerWithoutHooksMeansCompleted()
  {
    // The model ended its turn (stop reason end_turn): complete even without a Stop event.
    var turn = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).Text("msg_1", 5, "Done.").Project().Turns.Single();

    (turn.Status, turn.EndedAt).ShouldBe((TurnStatus.Completed, (DateTimeOffset?)At(5)));
  }

  [Fact]
  public void NextPromptWithoutAStopMeansInterrupted()
  {
    var turns = new ClaudeCodeSession()
      .PromptLine("p-1", "Go", 0).ToolUse("msg_1", 2, "toolu_1").PreToolUse("p-1", "toolu_1", 3)
      .PromptLine("p-2", "Stop that", 20)
      .Project().Turns;

    (turns[0].Status, turns[0].EndedAt).ShouldBe((TurnStatus.Interrupted, (DateTimeOffset?)At(3)));
  }

  [Fact]
  public void LastTurnWithoutAStopIsInProgress()
  {
    var turn = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).ToolUse("msg_1", 2, "toolu_1").Project().Turns.Single();

    (turn.Status, turn.EndedAt).ShouldBe((TurnStatus.InProgress, (DateTimeOffset?)null));
  }

  [Fact]
  public void TurnWithoutModelActivityIsCompleted()
  {
    // A local command (e.g. /model) never calls the model, so nothing can be interrupted.
    var turns = new ClaudeCodeSession()
      .PromptLine("p-1", "<command-name>/model</command-name>", 0, source: null)
      .PromptLine("p-2", "Go", 10)
      .Project().Turns;

    (turns[0].Status, turns[0].EndedAt).ShouldBe((TurnStatus.Completed, (DateTimeOffset?)At(0)));
  }

  [Theory]
  [InlineData("typed", "Typed")]
  [InlineData("queued", "Queued")]
  [InlineData("system", "Injected")]
  [InlineData(null, "Typed")]
  public void PromptSourceFromTheTranscript(string? source, string expected)
  {
    new ClaudeCodeSession().PromptLine("p-1", "Go", 0, source).Project().Turns.Single().Source.Name.ShouldBe(expected);
  }

  [Fact]
  public void OnlyTheFirstPromptLineIsThePrompt()
  {
    // A local command writes its output as further user lines with the same prompt ID.
    var turn = new ClaudeCodeSession()
      .PromptLine("p-1", "<command-name>/compact</command-name>", 0, source: null)
      .PromptLine("p-1", "<local-command-stdout>Compacted</local-command-stdout>", 1, source: null)
      .Project().Turns.Single();

    turn.Steps.Single().Text.ShouldBe("<command-name>/compact</command-name>");
  }

  [Fact]
  public void MetaAndCompactSummaryLinesAreNotPrompts()
  {
    var turn = new ClaudeCodeSession()
      .PromptLine("p-1", "Skill instructions", 0, source: null, isMeta: true)
      .PromptLine("p-1", "This session is being continued…", 1, source: null, isCompactSummary: true)
      .PromptLine("p-1", "Go", 2)
      .Project().Turns.Single();

    turn.Steps.Single().Text.ShouldBe("Go");
  }

  [Fact]
  public void ContextFromThePromptEvent()
  {
    var context = Context with { GitBranch = "feature/cart" };
    var turn = new ClaudeCodeSession().UserPromptSubmit("p-1", "Go", 0, context).Project().Turns.Single();

    turn.Context.ShouldBe(new TurnContext("github.com/acme/shop", "feature/cart", new string('a', 40), "/workspace", "/workspace",
      "devbox", "eric"));
  }

  [Fact]
  public void ContextFromTheTranscriptWithoutEvents()
  {
    var turn = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).Project().Turns.Single();

    turn.Context.ShouldBe(TurnContext.None with { GitBranch = "feature/cart", Directory = "/workspace/src" });
  }

  [Fact]
  public void TitleIsTheLatestAiTitle()
  {
    new ClaudeCodeSession().AiTitle("Cart bug").PromptLine("p-1", "Go", 0).AiTitle("Fix the cart total").Project()
      .Title.ShouldBe("Fix the cart total");
  }

  [Fact]
  public void WithoutAnAiTitleTheTitleIsTheFirstPrompt()
  {
    new ClaudeCodeSession().PromptLine("p-1", "Fix the cart\nand its tests", 0).PromptLine("p-2", "Now the docs", 9).Project()
      .Title.ShouldBe("Fix the cart");
  }

  [Fact]
  public void WithoutPromptsThereIsNoTitle()
  {
    new ClaudeCodeSession().SessionStart(0).Project().Title.ShouldBeNull();
  }

  [Fact]
  public void SessionWithoutPromptsHasNoTurns()
  {
    var session = new ClaudeCodeSession().SessionStart(0).Project();

    session.Turns.ShouldBeEmpty();
    (session.StartedAt, session.LastActivityAt).ShouldBe((At(0), At(0)));
  }

  [Fact]
  public void SessionSpansAllItsEvidence()
  {
    var session = new ClaudeCodeSession().SessionStart(0).PromptLine("p-1", "Go", 5).Text("msg_1", 8, "Done.").Stop("p-1", 9)
      .Project();

    (session.StartedAt, session.LastActivityAt).ShouldBe((At(0), At(9)));
  }
}
