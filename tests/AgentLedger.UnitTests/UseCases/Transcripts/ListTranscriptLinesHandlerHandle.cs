using AgentLedger.UseCases;
using AgentLedger.UseCases.Transcripts;
using AgentLedger.UseCases.Transcripts.ListTranscriptLines;
using Ardalis.Result;

namespace AgentLedger.UnitTests.UseCases.Transcripts;

// Listing transcript lines like raw events (ADR 0020, revised).
public sealed class ListTranscriptLinesHandlerHandle
{
  private static readonly DateTimeOffset T0 = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

  private readonly ITranscriptQueryService _queries = Substitute.For<ITranscriptQueryService>();
  private readonly ListTranscriptLinesHandler _handler;

  public ListTranscriptLinesHandlerHandle()
  {
    _queries.ListLinesAsync(Arg.Any<TranscriptLineFilter>(), Arg.Any<TranscriptLineCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(new TranscriptLinePage([], null));
    _handler = new ListTranscriptLinesHandler(_queries);
  }

  private ValueTask<Result<TranscriptLinePage>> Handle(TranscriptLineFilter filter, int limit = 50, TranscriptLineCursor? after = null) =>
    _handler.Handle(new ListTranscriptLinesQuery(filter, after, limit), CancellationToken.None);

  [Fact]
  public async Task PassesTheFilterAndCursorOn()
  {
    var filter = new TranscriptLineFilter(Agent: "ClaudeCode", LineType: "assistant", NativeSessionId: "s1", File: "subagents/agent-a1");
    var cursor = new TranscriptLineCursor(T0, 412, Guid.CreateVersion7());

    (await Handle(filter, 25, cursor)).IsSuccess.ShouldBeTrue();

    await _queries.Received(1).ListLinesAsync(filter, cursor, 25, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task NeedsNoFilters()
  {
    (await Handle(new TranscriptLineFilter())).IsSuccess.ShouldBeTrue();
  }

  [Theory]
  [InlineData("Cursor", null, false, 50, "agent")]
  [InlineData(null, "nope", false, 50, "file")]
  [InlineData(null, null, true, 50, "to")]
  [InlineData(null, null, false, 0, "limit")]
  [InlineData(null, null, false, Constants.MAX_PAGE_SIZE + 1, "limit")]
  public async Task RejectsUnusableParameters(string? agent, string? file, bool toBeforeFrom, int limit, string field)
  {
    var filter = new TranscriptLineFilter(Agent: agent, File: file, From: toBeforeFrom ? T0 : null, To: toBeforeFrom ? T0 : null);

    var result = await Handle(filter, limit);

    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe(field);
  }
}
