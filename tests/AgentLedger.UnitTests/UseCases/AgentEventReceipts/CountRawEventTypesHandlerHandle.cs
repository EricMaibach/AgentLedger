using AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;
using Ardalis.Result;

namespace AgentLedger.UnitTests.UseCases.AgentEventReceipts;

public sealed class CountRawEventTypesHandlerHandle
{
  private readonly IRawEventQueryService _service = Substitute.For<IRawEventQueryService>();
  private readonly CountRawEventTypesHandler _handler;

  public CountRawEventTypesHandlerHandle() => _handler = new CountRawEventTypesHandler(_service);

  [Fact]
  public async Task ReturnsTheCountsFromTheQueryService()
  {
    IReadOnlyList<EventTypeCount> counts = [new("PreToolUse", 42), new("Stop", 12)];
    var filter = new RawEventFilter(NativeSessionId: "s1");
    _service.CountEventTypesAsync(filter, Arg.Any<CancellationToken>()).Returns(counts);

    var result = await _handler.Handle(new CountRawEventTypesQuery(filter), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Ok);
    result.Value.ShouldBe(counts);
  }

  [Fact]
  public async Task RejectsAnUnknownAgent()
  {
    var result = await _handler.Handle(new CountRawEventTypesQuery(new RawEventFilter(Agent: "Cursor")), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("agent");
    _service.ReceivedCalls().ShouldBeEmpty();
  }
}
