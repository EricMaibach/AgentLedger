using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.UseCases;
using AgentLedger.UseCases.Transcripts;
using AgentLedger.UseCases.Transcripts.ListTranscriptFiles;
using AgentLedger.UseCases.Transcripts.ListTranscriptLines;
using Ardalis.Result;

namespace AgentLedger.UnitTests.UseCases.Transcripts;

public sealed class ListTranscriptFilesHandlerHandle
{
  private readonly ITranscriptQueryService _queries = Substitute.For<ITranscriptQueryService>();

  [Fact]
  public async Task ReturnsTheSessionsFiles()
  {
    IReadOnlyList<TranscriptFileItem> files = [new("ClaudeCode", "main", null, null, DateTimeOffset.UnixEpoch, 3)];
    _queries.ListFilesAsync("s1", Arg.Any<CancellationToken>()).Returns(files);

    var result = await new ListTranscriptFilesHandler(_queries).Handle(new ListTranscriptFilesQuery("s1"), CancellationToken.None);

    result.Value.ShouldBe(files);
  }

  [Fact]
  public async Task RequiresASession()
  {
    var result = await new ListTranscriptFilesHandler(_queries).Handle(new ListTranscriptFilesQuery(" "), CancellationToken.None);

    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("session");
  }
}

public sealed class ListTranscriptLinesHandlerHandle
{
  private readonly ITranscriptQueryService _queries = Substitute.For<ITranscriptQueryService>();
  private readonly ListTranscriptLinesHandler _handler;

  public ListTranscriptLinesHandlerHandle()
  {
    // Arg.Any doesn't match a Vogen struct, so match any arguments instead.
    _queries.ListLinesAsync("", TranscriptFileName.Main, null, 0, CancellationToken.None)
      .ReturnsForAnyArgs(new TranscriptLinePage([], null));
    _handler = new ListTranscriptLinesHandler(_queries);
  }

  [Fact]
  public async Task PagesThroughTheFileFromTheCursor()
  {
    var cursor = new TranscriptLineCursor(412, Guid.CreateVersion7());

    var result = await _handler.Handle(new ListTranscriptLinesQuery("s1", "subagents/agent-a1", cursor, 25), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _queries.Received(1).ListLinesAsync("s1", TranscriptFileName.ForSubagent("a1"), cursor, 25, Arg.Any<CancellationToken>());
  }

  [Theory]
  [InlineData(" ", "main", 10, "session")]
  [InlineData("s1", "nope", 10, "file")]
  [InlineData("s1", "main", 0, "limit")]
  [InlineData("s1", "main", Constants.MAX_PAGE_SIZE + 1, "limit")]
  public async Task RejectsInvalidParameters(string session, string file, int limit, string field)
  {
    var result = await _handler.Handle(new ListTranscriptLinesQuery(session, file, null, limit), CancellationToken.None);

    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe(field);
  }
}
