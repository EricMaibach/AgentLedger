using AgentLedger.UseCases.Work;
using AgentLedger.UseCases.Work.GetSession;
using AgentLedger.UseCases.Work.GetStep;
using AgentLedger.UseCases.Work.ListSessions;
using AgentLedger.UseCases.Work.ListSteps;
using AgentLedger.UseCases.Work.ListTurns;
using AgentLedger.UseCases.Work.LocateWork;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Work;

// The work pages' queries (ADR 0023): thin over IWorkQueryService, plus what needs the time (active sessions) and the
// not-found answers.
public sealed class WorkQueryHandlersHandle
{
  private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
  private readonly IWorkQueryService _queries = Substitute.For<IWorkQueryService>();
  private readonly FakeTimeProvider _clock = new(Now);

  [Fact]
  public async Task MarksSessionsActiveInTheLastTenMinutes()
  {
    _queries.ListSessionsAsync(null, 50, Arg.Any<CancellationToken>())
      .Returns(new SessionPage([Session(Now.AddMinutes(-10)), Session(Now.AddMinutes(-10).AddSeconds(-1))], null));

    var result = await new ListSessionsHandler(_queries, _clock).Handle(new ListSessionsQuery(null, 50), CancellationToken.None);

    result.Value.Items.Select(s => s.Active).ShouldBe([true, false]);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(101)]
  public async Task RefusesALimitOutOfRange(int limit)
  {
    var result = await new ListSessionsHandler(_queries, _clock).Handle(new ListSessionsQuery(null, limit), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Invalid);
  }

  [Fact]
  public async Task MarksTheSessionDetailActive()
  {
    var id = Guid.NewGuid();
    _queries.GetSessionAsync(id, Arg.Any<CancellationToken>())
      .Returns(new SessionDetail(Session(Now.AddMinutes(-1)), null, null, new Dictionary<string, int>(), 0, 0, []));

    var result = await new GetSessionHandler(_queries, _clock).Handle(new GetSessionQuery(id), CancellationToken.None);

    result.Value.Session.Active.ShouldBeTrue();
  }

  [Fact]
  public async Task AnswersNotFoundForUnknownIds()
  {
    var id = Guid.NewGuid();
    // Explicitly null: NSubstitute would otherwise return a substitute list.
    _queries.ListTurnsAsync(id, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<TurnListItem>?)null);
    _queries.ListStepsAsync(id, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<StepListItem>?)null);

    (await new GetSessionHandler(_queries, _clock).Handle(new GetSessionQuery(id), CancellationToken.None)).Status
      .ShouldBe(ResultStatus.NotFound);
    (await new ListTurnsHandler(_queries).Handle(new ListTurnsQuery(id), CancellationToken.None)).Status
      .ShouldBe(ResultStatus.NotFound);
    (await new ListStepsHandler(_queries).Handle(new ListStepsQuery(id), CancellationToken.None)).Status
      .ShouldBe(ResultStatus.NotFound);
    (await new GetStepHandler(_queries).Handle(new GetStepQuery(id), CancellationToken.None)).Status
      .ShouldBe(ResultStatus.NotFound);
    (await new LocateTurnHandler(_queries).Handle(new LocateTurnQuery(id), CancellationToken.None)).Status
      .ShouldBe(ResultStatus.NotFound);
    (await new LocateStepHandler(_queries).Handle(new LocateStepQuery(id), CancellationToken.None)).Status
      .ShouldBe(ResultStatus.NotFound);
  }

  [Fact]
  public async Task LocatesTurnsAndSteps()
  {
    var (turn, step, location) = (Guid.NewGuid(), Guid.NewGuid(), new WorkLocation(Guid.NewGuid(), Guid.NewGuid()));
    _queries.LocateTurnAsync(turn, Arg.Any<CancellationToken>()).Returns(location);
    _queries.LocateStepAsync(step, Arg.Any<CancellationToken>()).Returns(location with { SessionId = Guid.Empty });

    (await new LocateTurnHandler(_queries).Handle(new LocateTurnQuery(turn), CancellationToken.None)).Value.ShouldBe(location);
    (await new LocateStepHandler(_queries).Handle(new LocateStepQuery(step), CancellationToken.None)).Value.SessionId
      .ShouldBe(Guid.Empty);
  }

  private static SessionListItem Session(DateTimeOffset lastActivity) =>
    new(Guid.NewGuid(), "ClaudeCode", "s", "Title", null, null, 1, lastActivity, lastActivity);
}
