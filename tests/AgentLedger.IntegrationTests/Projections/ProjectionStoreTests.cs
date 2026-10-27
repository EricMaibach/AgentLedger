using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.Infrastructure.Data;
using AgentLedger.UseCases.Projections;

namespace AgentLedger.IntegrationTests.Projections;

// Storing projected sessions (ADR 0022) against real Postgres. Each test uses its own session.
[Collection(nameof(PostgresCollection))]
public sealed class ProjectionStoreTests(PostgresFixture db)
{
  private static readonly DateTimeOffset T0 = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  [Fact]
  public async Task StoresASessionWithEveryValue()
  {
    var session = Session(Turn("p-1", 0));

    await ReplaceAsync(session, version: 3);

    using var scope = db.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var row = await context.Sessions.SingleAsync(s => s.Id == session.Id.Value);
    (row.Agent, row.NativeSessionId, row.StartedAt, row.LastActivityAt, row.TurnCount, row.ProjectionVersion)
      .ShouldBe(("ClaudeCode", _session, T0, T0.AddMinutes(9), 1, 3));
    row.Title.ShouldBe("Fix the cart");
    // Where the session was last working: its latest turn's context, so lists need no join.
    (row.GitRepo, row.GitBranch, row.Host, row.User).ShouldBe(("github.com/acme/shop", "main", "devbox", "eric"));

    var turn = await context.Turns.SingleAsync(t => t.SessionId == row.Id);
    var expected = session.Turns[0];
    (turn.Id, turn.Sequence, turn.PromptId, turn.Status, turn.Source, turn.StartedAt, turn.EndedAt)
      .ShouldBe((expected.Id.Value, 0, "p-1", "Completed", "Queued", T0, (DateTimeOffset?)T0.AddMinutes(1)));
    (turn.GitRepo, turn.GitBranch, turn.GitCommit, turn.GitWorktree, turn.Directory, turn.Host, turn.User)
      .ShouldBe(("github.com/acme/shop", "main", new string('a', 40), "/w", "/w/src", "devbox", "eric"));
    turn.SourceEventIds.ShouldBe(expected.Lineage.EventIds.Select(e => e.Value).ToArray());
    turn.SourceLineIds.ShouldBe(expected.Lineage.LineIds.Select(l => l.Value).ToArray());

    var steps = await context.Steps.Where(s => s.TurnId == turn.Id).OrderBy(s => s.Sequence).ToListAsync();
    steps.Select(s => s.Kind).ShouldBe(["Prompt", "Thinking", "ToolCall"]);
    steps.ShouldAllBe(s => s.SessionId == row.Id);
    var call = steps[2];
    (call.Id, call.Sequence, call.ToolName, call.ToolUseId, call.ToolInput, call.ToolOutput, call.Succeeded)
      .ShouldBe((expected.Steps[2].Id.Value, 2, "Bash", "toolu_1", """{"command":"ls"}""", "a.txt", (bool?)true));
    (call.StartedAt, call.EndedAt, call.ResponseId).ShouldBe((T0.AddSeconds(2), (DateTimeOffset?)T0.AddSeconds(3),
      (Guid?)expected.Responses[0].Id.Value));
    steps[1].Text.ShouldBeNull();
    steps[0].Text.ShouldBe("Go");
    (steps[0].Summary, steps[1].Summary, steps[2].Summary).ShouldBe(("Go", null, "ls"));

    var response = await context.ModelResponses.SingleAsync(r => r.TurnId == turn.Id);
    (response.MessageId, response.Model, response.RespondedAt, response.SessionId).ShouldBe(("msg_1", "claude-opus-5-5",
      T0.AddSeconds(1), row.Id));
    (response.InputTokens, response.OutputTokens, response.CacheWrite5mTokens, response.CacheWrite1hTokens,
      response.CacheReadTokens, response.ThinkingTokens, response.WebSearches, response.WebFetches)
      .ShouldBe((2L, 836L, 5L, 56L, 746_742L, 81L, 1L, 2L));
  }

  [Fact]
  public async Task ReplacesEverythingStoredForTheSession()
  {
    await ReplaceAsync(Session(Turn("p-1", 0), Turn("p-2", 10)), version: 1);

    await ReplaceAsync(Session(Turn("p-2", 10)), version: 2);

    using var scope = db.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var sessionId = SessionId.For(AgentKind.ClaudeCode, _session).Value;
    (await context.Sessions.SingleAsync(s => s.Id == sessionId)).ProjectionVersion.ShouldBe(2);
    (await context.Turns.Where(t => t.SessionId == sessionId).Select(t => t.PromptId).ToListAsync()).ShouldBe(["p-2"]);
    (await context.Steps.CountAsync(s => s.SessionId == sessionId)).ShouldBe(3);
    (await context.ModelResponses.CountAsync(r => r.SessionId == sessionId)).ShouldBe(1);
  }

  [Fact]
  public async Task SessionContextIsItsLatestTurns()
  {
    var later = Turn("p-2", 10);
    later = new Turn(later.Id, later.PromptId, later.Source, later.Status, later.StartedAt, later.EndedAt,
      later.Context with { GitBranch = "feature/cart" }, later.Steps, later.Responses);

    await ReplaceAsync(Session(Turn("p-1", 0), later), version: 1);

    using var scope = db.Services.CreateScope();
    (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Sessions.SingleAsync(s => s.NativeSessionId == _session))
      .GitBranch.ShouldBe("feature/cart");
  }

  [Fact]
  public async Task ReplacingIsRepeatable()
  {
    var session = Session(Turn("p-1", 0));
    await ReplaceAsync(session, version: 1);

    await ReplaceAsync(session, version: 1);

    using var scope = db.Services.CreateScope();
    (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Turns.CountAsync(t => t.SessionId == session.Id.Value))
      .ShouldBe(1);
  }

  [Fact]
  public async Task KeepsTextWithCharactersPostgresCannotStore()
  {
    // Postgres text can't hold NUL; tool output can (seen in real transcripts). Shown as the replacement character.
    var turn = new Turn(TurnId.For(AgentKind.ClaudeCode, _session, "p-1"), "p-1", PromptSource.Typed, TurnStatus.Completed, T0,
      T0, TurnContext.None,
      [Step.Prompt(StepId.For(AgentKind.ClaudeCode, _session, StepKind.Prompt, "p-1"), T0, "a\0b", Lineage(1))], []);

    await ReplaceAsync(Session(turn), version: 1);

    using var scope = db.Services.CreateScope();
    (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Steps.SingleAsync(s => s.TurnId == turn.Id.Value))
      .Text.ShouldBe("a\uFFFDb");
  }

  [Fact]
  public async Task ListsSessionsBuiltByAnOlderVersion()
  {
    var store = Store(out var scope);
    using var _ = scope;
    var outdated = Session(Turn("p-1", 0));
    await store.ReplaceSessionAsync(outdated, 1_000_001, CancellationToken.None);
    var current = new ProjectedSession(AgentKind.ClaudeCode, $"session-{Guid.NewGuid():N}", T0, T0, []);
    await store.ReplaceSessionAsync(current, 1_000_002, CancellationToken.None);

    // Other tests store sessions at low versions; ask about versions only these two use.
    var listed = await store.ListOutdatedSessionsAsync(1_000_002, 1000, CancellationToken.None);

    listed.ShouldContain(new SessionKey(AgentKind.ClaudeCode, _session));
    listed.ShouldNotContain(new SessionKey(AgentKind.ClaudeCode, current.NativeSessionId));
  }

  [Fact]
  public async Task KeepsTheCheckpoint()
  {
    var store = Store(out var scope);
    using var _ = scope;
    var checkpoint = new ProjectionCheckpoint(T0.AddHours(1), 4);

    await store.SaveCheckpointAsync(checkpoint, CancellationToken.None);
    await store.SaveCheckpointAsync(checkpoint with { AppliedVersion = 5 }, CancellationToken.None);

    (await store.GetCheckpointAsync(CancellationToken.None)).ShouldBe(checkpoint with { AppliedVersion = 5 });
  }

  private ProjectedSession Session(params Turn[] turns) =>
    new(AgentKind.ClaudeCode, _session, T0, T0.AddMinutes(9), turns, "Fix the cart");

  private Turn Turn(string promptId, int startMinute)
  {
    var at = T0.AddMinutes(startMinute);
    var responseId = ModelResponseId.For(AgentKind.ClaudeCode, _session, "msg_1" + promptId);
    var response = new ModelResponse(responseId, "msg_1", "claude-opus-5-5", at.AddSeconds(1),
      new TokenUsage(2, 836, 5, 56, 746_742, 81, 1, 2), Lineage(5));
    Step[] steps =
    [
      Step.Prompt(StepId.For(AgentKind.ClaudeCode, _session, StepKind.Prompt, promptId), at, "Go", Lineage(1)),
      Step.Thinking(StepId.For(AgentKind.ClaudeCode, _session, StepKind.Thinking, promptId + "#0"), at.AddSeconds(1), null,
        responseId, Lineage(2)),
      Step.ToolCall(StepId.For(AgentKind.ClaudeCode, _session, StepKind.ToolCall, "toolu_1" + (promptId == "p-1" ? "" : promptId)),
        at.AddSeconds(2), "Bash", "toolu_1", """{"command":"ls"}""", "a.txt", true, at.AddSeconds(3), responseId, Lineage(3), "ls"),
    ];
    var context = new TurnContext("github.com/acme/shop", "main", new string('a', 40), "/w", "/w/src", "devbox", "eric");
    return new Turn(TurnId.For(AgentKind.ClaudeCode, _session, promptId), promptId, PromptSource.Queued, TurnStatus.Completed, at,
      at.AddMinutes(1), context, steps, [response]);
  }

  private static Lineage Lineage(int seed) =>
    new([AgentEventId.From(new Guid(seed, 0, 0, new byte[8]))],
      [TranscriptLineId.From(new Guid(seed, 1, 0, new byte[8]))]);

  private async Task ReplaceAsync(ProjectedSession session, int version)
  {
    var store = Store(out var scope);
    using var _ = scope;
    await store.ReplaceSessionAsync(session, version, CancellationToken.None);
  }

  private IProjectionStore Store(out IServiceScope scope)
  {
    scope = db.Services.CreateScope();
    return scope.ServiceProvider.GetRequiredService<IProjectionStore>();
  }
}
