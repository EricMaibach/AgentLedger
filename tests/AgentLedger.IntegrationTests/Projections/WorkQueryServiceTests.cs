using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.IntegrationTests.Data;
using AgentLedger.UseCases.Projections;
using AgentLedger.UseCases.Transcripts;
using AgentLedger.UseCases.Work;

namespace AgentLedger.IntegrationTests.Projections;

// Reading projections for the work pages (ADR 0023) against real Postgres.
[Collection(nameof(PostgresCollection))]
public sealed class WorkQueryServiceTests(PostgresFixture db)
{
  private static readonly AgentKind Agent = AgentKind.ClaudeCode;
  // Far in the future, so these sessions list before every other test's.
  private static readonly DateTimeOffset T0 = new(2099, 1, 1, 12, 0, 0, TimeSpan.Zero);
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  [Fact]
  public async Task ListsSessionsMostRecentFirstInPages()
  {
    var start = T0.AddDays(Random.Shared.Next(1, 10_000));
    var names = new[] { "older", "middle", "newest" }.Select(n => $"{n}-{Guid.NewGuid():N}").ToList();
    for (var i = 0; i < names.Count; i++)
      await StoreAsync(new ProjectedSession(Agent, names[i], start, start.AddMinutes(i), [], $"Title {i}"));

    var first = await Service().ListSessionsAsync(null, 2, CancellationToken.None);
    var second = await Service().ListSessionsAsync(first.Next, 2, CancellationToken.None);

    first.Items.Select(s => s.NativeSessionId).ShouldBe([names[2], names[1]]);
    first.Next.ShouldNotBeNull();
    second.Items[0].NativeSessionId.ShouldBe(names[0]);
    second.Items[0].Title.ShouldBe("Title 0");
  }

  [Fact]
  public async Task GetsASessionWithItsTotals()
  {
    var session = await StoreAsync(Session(
      Turn("p-1", 0, TurnStatus.Completed, ToolCall("toolu_1", true), ToolCall("toolu_2", false)),
      Turn("p-2", 10, TurnStatus.Failed, ToolCall("toolu_3", null))));

    var detail = await Service().GetSessionAsync(session.Id.Value, CancellationToken.None);

    detail.ShouldNotBeNull();
    detail.Session.Title.ShouldBe("Fix the cart");
    (detail.Session.GitRepo, detail.Session.GitBranch, detail.Host, detail.User).ShouldBe(("github.com/acme/shop", "main", "devbox", "eric"));
    detail.TurnsByStatus.ShouldBe(new Dictionary<string, int> { ["Completed"] = 1, ["Failed"] = 1 }, ignoreOrder: true);
    (detail.ToolCalls, detail.FailedToolCalls).ShouldBe((3, 1));
    detail.TokensByModel.ShouldBe([new ModelTokens("claude-opus-5-5", 2, 4, 1672, 122, 1_493_484, 162)]);
  }

  [Fact]
  public async Task GetsNothingForAnUnknownSession()
  {
    (await Service().GetSessionAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    (await Service().ListTurnsAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    (await Service().ListStepsAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    (await Service().GetStepAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    (await Service().LocateTurnAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    (await Service().LocateStepAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
  }

  [Fact]
  public async Task ListsTurnsInOrderWithTheirToolCounts()
  {
    var session = await StoreAsync(Session(
      Turn("p-1", 0, TurnStatus.Completed, ToolCall("toolu_1", true), ToolCall("toolu_2", false)),
      Turn("p-2", 10, TurnStatus.InProgress)));

    var turns = await Service().ListTurnsAsync(session.Id.Value, CancellationToken.None);

    turns.ShouldNotBeNull();
    turns.Select(t => (t.Number, t.Status, t.Source, t.PromptSummary, t.ToolCalls, t.FailedToolCalls))
      .ShouldBe([(1, "Completed", "Typed", "Fix p-1", 2, 1), (2, "InProgress", "Typed", "Fix p-2", 0, 0)]);
    turns[0].TurnId.ShouldBe(session.Turns[0].Id.Value);
    (turns[0].StartedAt, turns[0].EndedAt, turns[1].EndedAt).ShouldBe((T0, (DateTimeOffset?)T0.AddMinutes(1), null));
  }

  [Fact]
  public async Task ListsATurnsSteps()
  {
    var session = await StoreAsync(Session(Turn("p-1", 0, TurnStatus.Completed, ToolCall("toolu_1", false))));

    var steps = await Service().ListStepsAsync(session.Turns[0].Id.Value, CancellationToken.None);

    steps.ShouldNotBeNull();
    steps.Select(s => (s.Kind, s.Summary, s.ToolName, s.Succeeded))
      .ShouldBe([("Prompt", "Fix p-1", null, null), ("Thinking", null, null, null), ("ToolCall", "ls", "Bash", (bool?)false)]);
  }

  [Fact]
  public async Task GetsAStepWithItsResponseAndEvidence()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var line = new TranscriptLine(Agent, _session, TranscriptFileName.Main, 412, """{"type":"assistant"}""", key.Id, T0);
    using (var scope = db.Services.CreateScope())
    {
      await scope.ServiceProvider.GetRequiredService<ITranscriptStore>().AddAsync(
        new TranscriptFile(Agent, _session, TranscriptFileName.Main, null, key.Id, T0), [line], CancellationToken.None);
      await scope.ServiceProvider.GetRequiredService<IRepository<AgentEventReceipt>>().AddAsync(Receipt(EventOf(3), "PreToolUse"));
    }
    var lineage = new Lineage([EventOf(3)], [line.Id]);
    var session = await StoreAsync(Session(Turn("p-1", 0, TurnStatus.Completed, ToolCall("toolu_1", true, lineage))));
    var stepId = session.Turns[0].Steps[2].Id.Value;

    var step = await Service().GetStepAsync(stepId, CancellationToken.None);

    step.ShouldNotBeNull();
    (step.Kind, step.ToolName, step.ToolInput, step.ToolOutput, step.Succeeded).ShouldBe(("ToolCall", "Bash", """{"command":"ls"}""",
      "a.txt", (bool?)true));
    (step.TurnNumber, step.TurnStartedAt, step.TurnStatus, step.TurnSource).ShouldBe((1, T0, "Completed", "Typed"));
    step.Location.ShouldBe(new WorkLocation(session.Id.Value, session.Turns[0].Id.Value));
    step.Response.ShouldBe(new ResponseTokens("claude-opus-5-5", 2, 836, 5, 56, 746_742, 81));
    step.EvidenceEvents.ShouldBe([new EvidenceEvent(EventOf(3).Value, "PreToolUse", T0)]);
    step.EvidenceLines.ShouldBe([new EvidenceLine(line.Id.Value, "assistant", "main", 412)]);
  }

  [Fact]
  public async Task LocatesTurnsAndSteps()
  {
    var session = await StoreAsync(Session(Turn("p-1", 0, TurnStatus.Completed)));
    var turn = session.Turns[0];

    (await Service().LocateTurnAsync(turn.Id.Value, CancellationToken.None)).ShouldBe(new WorkLocation(session.Id.Value, turn.Id.Value));
    (await Service().LocateStepAsync(turn.Steps[0].Id.Value, CancellationToken.None))
      .ShouldBe(new WorkLocation(session.Id.Value, turn.Id.Value));
  }

  private ProjectedSession Session(params Turn[] turns) => new(Agent, _session, T0, T0.AddMinutes(20), turns, "Fix the cart");

  private Turn Turn(string promptId, int startMinute, TurnStatus status, params Func<DateTimeOffset, ModelResponseId, Step>[] tools)
  {
    var at = T0.AddMinutes(startMinute);
    var responseId = ModelResponseId.For(Agent, _session, "msg-" + promptId);
    var response = new ModelResponse(responseId, "msg-" + promptId, "claude-opus-5-5", at.AddSeconds(1),
      new TokenUsage(2, 836, 5, 56, 746_742, 81, 0, 0), Lineage(5));
    var steps = new List<Step>
    {
      Step.Prompt(StepId.For(Agent, _session, StepKind.Prompt, promptId), at, $"Fix {promptId}", Lineage(1)),
      Step.Thinking(StepId.For(Agent, _session, StepKind.Thinking, promptId), at.AddSeconds(1), null, responseId, Lineage(2)),
    };
    steps.AddRange(tools.Select(t => t(at, responseId)));
    var context = new TurnContext("github.com/acme/shop", "main", null, "/w", "/w", "devbox", "eric");
    return new Turn(TurnId.For(Agent, _session, promptId), promptId, PromptSource.Typed, status, at,
      status == TurnStatus.InProgress ? null : at.AddMinutes(1), context, steps, [response]);
  }

  private Func<DateTimeOffset, ModelResponseId, Step> ToolCall(string toolUseId, bool? succeeded, Lineage? lineage = null) =>
    (at, responseId) => Step.ToolCall(StepId.For(Agent, _session, StepKind.ToolCall, toolUseId), at.AddSeconds(2), "Bash", toolUseId,
      """{"command":"ls"}""", "a.txt", succeeded, succeeded is null ? null : at.AddSeconds(3), responseId, lineage ?? Lineage(3), "ls");

  private static AgentEventId EventOf(int seed) => AgentEventId.From(new Guid(seed, 7, 7, new byte[8]));

  private static Lineage Lineage(int seed) => new([AgentEventId.From(new Guid(seed, 0, 0, new byte[8]))], []);

  private AgentEventReceipt Receipt(AgentEventId eventId, string type) =>
    new(ReceiptId.From(Guid.CreateVersion7()), eventId, Agent, type, _session, T0, T0,
      new CaptureContext("devbox", "eric", "/w", null, null, null), new Dictionary<string, string>(), "{}");

  private async Task<ProjectedSession> StoreAsync(ProjectedSession session)
  {
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IProjectionStore>().ReplaceSessionAsync(session, 1, CancellationToken.None);
    return session;
  }

  private IWorkQueryService Service() => db.Services.CreateScope().ServiceProvider.GetRequiredService<IWorkQueryService>();
}
