using AgentLedger.UseCases;
using AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;
using Ardalis.Result;

namespace AgentLedger.UnitTests.UseCases.AgentEventReceipts;

public sealed class ListRawEventsHandlerHandle
{
  private static readonly RawEventPage SomePage = new([], Next: null);

  private readonly IRawEventQueryService _service = Substitute.For<IRawEventQueryService>();
  private readonly ListRawEventsHandler _handler;

  public ListRawEventsHandlerHandle()
  {
    _service.ListAsync(default!, default, default, default, default).ReturnsForAnyArgs(SomePage);
    _handler = new ListRawEventsHandler(_service);
  }

  private static ListRawEventsQuery Query(
    RawEventFilter? filter = null, bool distinct = true, RawEventCursor? after = null, int limit = Constants.DEFAULT_PAGE_SIZE) =>
    new(filter ?? new RawEventFilter(), distinct, after, limit);

  [Fact]
  public async Task ReturnsThePageFromTheQueryService()
  {
    var filter = new RawEventFilter(Agent: "ClaudeCode", NativeSessionId: "s1");
    var after = new RawEventCursor(DateTimeOffset.UnixEpoch, Guid.CreateVersion7());

    var result = await _handler.Handle(Query(filter, distinct: false, after, limit: 25), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Ok);
    result.Value.ShouldBeSameAs(SomePage);
    await _service.Received(1).ListAsync(filter, false, after, 25, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task RejectsAnUnknownAgent()
  {
    var result = await _handler.Handle(Query(new RawEventFilter(Agent: "Cursor")), CancellationToken.None);

    ShouldBeInvalidWithoutQuerying(result, "agent");
  }

  [Theory]
  [InlineData(0)]
  [InlineData(Constants.MAX_PAGE_SIZE + 1)]
  public async Task RejectsALimitOutsideTheAllowedRange(int limit)
  {
    var result = await _handler.Handle(Query(limit: limit), CancellationToken.None);

    ShouldBeInvalidWithoutQuerying(result, "limit");
  }

  [Fact]
  public async Task RejectsATimeRangeThatEndsBeforeItStarts()
  {
    var from = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    var result = await _handler.Handle(Query(new RawEventFilter(From: from, To: from.AddHours(-1))), CancellationToken.None);

    ShouldBeInvalidWithoutQuerying(result, "to");
  }

  private void ShouldBeInvalidWithoutQuerying(Result<RawEventPage> result, string field)
  {
    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe(field);
    _service.ReceivedCalls().ShouldBeEmpty();
  }
}
