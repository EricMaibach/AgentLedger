using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using AgentLedger.UseCases.Projections;
using AgentLedger.UseCases.Projections.RunProjections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Projections;

// One pass of the projection worker (ADR 0022): rebuild every session with new raw rows, then a batch of sessions
// built by an older projection version.
public sealed class RunProjectionsHandlerHandle
{
  private static readonly DateTimeOffset Now = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
  private static readonly TimeSpan Lag = TimeSpan.FromSeconds(5);
  private static readonly SessionKey A = new(AgentKind.ClaudeCode, "session-a");
  private static readonly SessionKey B = new(AgentKind.ClaudeCode, "session-b");

  private readonly IProjectionSourceQuery _source = Substitute.For<IProjectionSourceQuery>();
  private readonly IProjectionStore _store = Substitute.For<IProjectionStore>();
  private readonly IAgentAdapter _adapter = Substitute.For<IAgentAdapter>();
  private readonly FakeTimeProvider _clock = new(Now);

  public RunProjectionsHandlerHandle()
  {
    _adapter.Agent.Returns(AgentKind.ClaudeCode);
    _adapter.Project(Arg.Any<SessionEvidence>()).Returns(c => Projected(c.Arg<SessionEvidence>().NativeSessionId));
    _source.ReadSessionAsync(Arg.Any<SessionKey>(), Arg.Any<CancellationToken>())
      .Returns(c => Evidence(c.Arg<SessionKey>()));
    _store.GetCheckpointAsync(Arg.Any<CancellationToken>()).Returns(new ProjectionCheckpoint(Now.AddMinutes(-1), ProjectionVersion.Current));
    _store.ListOutdatedSessionsAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
    Changed();
  }

  [Fact]
  public async Task ProjectsSessionsWithNewRowsAndMovesTheWatermark()
  {
    Changed(A, B);

    var summary = await HandleAsync();

    await _source.Received().FindChangedSessionsAsync(Now.AddMinutes(-1), Now - Lag, Arg.Any<CancellationToken>());
    await _store.Received().ReplaceSessionAsync(Arg.Is<ProjectedSession>(s => s.NativeSessionId == "session-a"),
      ProjectionVersion.Current, Arg.Any<CancellationToken>());
    await _store.Received().ReplaceSessionAsync(Arg.Is<ProjectedSession>(s => s.NativeSessionId == "session-b"),
      ProjectionVersion.Current, Arg.Any<CancellationToken>());
    await _store.Received().SaveCheckpointAsync(new ProjectionCheckpoint(Now - Lag, ProjectionVersion.Current),
      Arg.Any<CancellationToken>());
    summary.Projected.ShouldBe(2);
  }

  [Fact]
  public async Task FirstPassReadsEverything()
  {
    _store.GetCheckpointAsync(Arg.Any<CancellationToken>()).Returns(ProjectionCheckpoint.Initial);

    await HandleAsync();

    await _source.Received().FindChangedSessionsAsync(null, Now - Lag, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task ASessionTheAdapterCannotProjectDoesNotStopTheOthers()
  {
    // Bad data or an adapter bug: logged and skipped; it's retried when its session changes or the version does.
    Changed(A, B);
    _adapter.Project(Arg.Is<SessionEvidence>(e => e.NativeSessionId == "session-a")).Returns(_ => throw new ArgumentException("bad"));

    var summary = await HandleAsync();

    await _store.Received(1).ReplaceSessionAsync(Arg.Any<ProjectedSession>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    (summary.Projected, summary.Failed).ShouldBe((1, 1));
    await _store.Received().SaveCheckpointAsync(Arg.Any<ProjectionCheckpoint>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task AStorageFailureStopsThePassWithoutMovingTheWatermark()
  {
    // The database is unavailable: the whole window is retried on the next pass.
    Changed(A);
    _store.ReplaceSessionAsync(Arg.Any<ProjectedSession>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(_ => throw new InvalidOperationException("connection lost"));

    await Assert.ThrowsAsync<InvalidOperationException>(HandleAsync);

    await _store.DidNotReceive().SaveCheckpointAsync(Arg.Any<ProjectionCheckpoint>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task SkipsSessionsOfAgentsWithoutAnAdapter()
  {
    Changed(new SessionKey(AgentKind.Codex, "codex-session"));

    var summary = await HandleAsync();

    await _store.DidNotReceive().ReplaceSessionAsync(Arg.Any<ProjectedSession>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    summary.Projected.ShouldBe(0);
  }

  [Fact]
  public async Task SkipsSessionsWithNothingStored()
  {
    Changed(A);
    _source.ReadSessionAsync(A, Arg.Any<CancellationToken>()).Returns((SessionEvidence?)null);

    (await HandleAsync()).Projected.ShouldBe(0);
  }

  [Fact]
  public async Task RebuildsABatchOfOutdatedSessions()
  {
    _store.GetCheckpointAsync(Arg.Any<CancellationToken>()).Returns(new ProjectionCheckpoint(Now, ProjectionVersion.Current - 1));
    var outdated = Enumerable.Range(0, RunProjectionsHandler.RebuildBatchSize)
      .Select(i => new SessionKey(AgentKind.ClaudeCode, $"old-{i}")).ToList();
    _store.ListOutdatedSessionsAsync(ProjectionVersion.Current, RunProjectionsHandler.RebuildBatchSize, Arg.Any<CancellationToken>())
      .Returns(outdated);

    var summary = await HandleAsync();

    summary.Rebuilt.ShouldBe(RunProjectionsHandler.RebuildBatchSize);
    // A full batch: there may be more, so the version isn't applied yet.
    await _store.Received().SaveCheckpointAsync(new ProjectionCheckpoint(Now - Lag, ProjectionVersion.Current - 1),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task RecordsTheVersionOnceNoOutdatedSessionsRemain()
  {
    _store.GetCheckpointAsync(Arg.Any<CancellationToken>()).Returns(new ProjectionCheckpoint(Now, ProjectionVersion.Current - 1));
    _store.ListOutdatedSessionsAsync(ProjectionVersion.Current, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([A]);

    await HandleAsync();

    await _store.Received().SaveCheckpointAsync(new ProjectionCheckpoint(Now - Lag, ProjectionVersion.Current),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task NoRebuildWhenTheVersionIsApplied()
  {
    await HandleAsync();

    await _store.DidNotReceive().ListOutdatedSessionsAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
  }

  private void Changed(params SessionKey[] keys) =>
    _source.FindChangedSessionsAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
      .Returns(keys);

  private async Task<ProjectionRunSummary> HandleAsync()
  {
    var handler = new RunProjectionsHandler(_source, _store, [_adapter], _clock, NullLogger<RunProjectionsHandler>.Instance);
    var result = await handler.Handle(new RunProjectionsCommand(Lag), CancellationToken.None);
    return result.Value;
  }

  private static SessionEvidence Evidence(SessionKey key) => new(key.Agent, key.NativeSessionId, [], []);

  private static ProjectedSession Projected(string session) => new(AgentKind.ClaudeCode, session, Now, Now, []);
}
