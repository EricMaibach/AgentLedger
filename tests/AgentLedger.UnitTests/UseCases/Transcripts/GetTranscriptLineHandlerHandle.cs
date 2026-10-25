using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.UseCases.ApiKeys;
using AgentLedger.UseCases.Transcripts;
using AgentLedger.UseCases.Transcripts.GetTranscriptLine;
using Ardalis.Result;

namespace AgentLedger.UnitTests.UseCases.Transcripts;

// One transcript line with who sent it (ADR 0019, 0020).
public sealed class GetTranscriptLineHandlerHandle
{
  private readonly ITranscriptQueryService _queries = Substitute.For<ITranscriptQueryService>();
  private readonly IApiKeyQueryService _apiKeys = Substitute.For<IApiKeyQueryService>();
  private readonly GetTranscriptLineHandler _handler;

  public GetTranscriptLineHandlerHandle() => _handler = new GetTranscriptLineHandler(_queries, _apiKeys);

  [Fact]
  public async Task ReturnsTheLineWithItsSender()
  {
    var lineId = Guid.CreateVersion7();
    var keyId = Guid.CreateVersion7();
    var row = new TranscriptLineRow(lineId, "ClaudeCode", "user", "s1", "main", null, 0, "{}", DateTimeOffset.UnixEpoch, keyId, null);
    var sender = new ApiKeySender(keyId, "laptop", "eric");
    // Arg.Any doesn't match a Vogen struct, so match any arguments instead.
    _queries.GetLineAsync(TranscriptLineId.From(lineId), CancellationToken.None).ReturnsForAnyArgs(row);
    _apiKeys.GetSendersAsync(Arg.Any<IReadOnlyCollection<ApiKeyId>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ApiKeyId, ApiKeySender> { [ApiKeyId.From(keyId)] = sender });

    var result = await _handler.Handle(new GetTranscriptLineQuery(lineId), CancellationToken.None);

    result.Value.ShouldBe(new TranscriptLineDetail(row, sender));
  }

  [Fact]
  public async Task IsNotFoundForAnUnknownLine()
  {
    _queries.GetLineAsync(TranscriptLineId.From(Guid.CreateVersion7()), CancellationToken.None).ReturnsForAnyArgs((TranscriptLineRow?)null);

    (await _handler.Handle(new GetTranscriptLineQuery(Guid.CreateVersion7()), CancellationToken.None)).Status.ShouldBe(ResultStatus.NotFound);
  }

  [Fact]
  public async Task RejectsAnEmptyId()
  {
    var result = await _handler.Handle(new GetTranscriptLineQuery(Guid.Empty), CancellationToken.None);

    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("lineId");
  }
}
