using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.UseCases;
using AgentLedger.UseCases.Transcripts;
using AgentLedger.UseCases.Transcripts.IngestTranscriptBatch;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Transcripts;

// One chunk of one transcript file (ADR 0020).
public sealed class IngestTranscriptBatchHandlerHandle
{
  private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
  private static readonly Guid KeyId = Guid.CreateVersion7();

  private readonly ITranscriptStore _store = Substitute.For<ITranscriptStore>();
  private readonly IngestTranscriptBatchHandler _handler;
  private TranscriptFile? _file;
  private IReadOnlyList<TranscriptLine>? _lines;

  public IngestTranscriptBatchHandlerHandle()
  {
    StoreReportsNew(int.MaxValue);
    _handler = new IngestTranscriptBatchHandler(_store, new FakeTimeProvider(Now));
  }

  // The store captures what it was given and says how many lines were new (at most all of them).
  private void StoreReportsNew(int stored) =>
    _store.AddAsync(Arg.Do<TranscriptFile>(f => _file = f), Arg.Do<IReadOnlyList<TranscriptLine>>(l => _lines = l), Arg.Any<CancellationToken>())
      .Returns(call => Math.Min(stored, call.Arg<IReadOnlyList<TranscriptLine>>().Count));

  private static IngestTranscriptBatchCommand Command(string file = "main", string? meta = null, IReadOnlyList<TranscriptLineInput>? lines = null,
    string agent = "ClaudeCode", string session = "s1") =>
    new(agent, session, file, meta, lines ?? [new(0, """{"type":"user"}"""), new(16, """{"type":"assistant"}""")], KeyId);

  private ValueTask<Result<TranscriptBatchStored>> Handle(IngestTranscriptBatchCommand command) => _handler.Handle(command, CancellationToken.None);

  [Fact]
  public async Task StoresTheLinesStampedWithArrivalAndKey()
  {
    var result = await Handle(Command(file: "subagents/agent-a1b2c3", meta: """{"agentType":"Explore"}"""));

    result.IsSuccess.ShouldBeTrue();
    _file.ShouldNotBeNull();
    _file.File.ShouldBe(TranscriptFileName.ForSubagent("a1b2c3"));
    _file.Meta.ShouldBe("""{"agentType":"Explore"}""");
    _file.ApiKeyId.ShouldBe(ApiKeyId.From(KeyId));
    _lines.ShouldNotBeNull();
    _lines.Select(l => (l.ByteOffset, l.Text)).ShouldBe([(0L, """{"type":"user"}"""), (16L, """{"type":"assistant"}""")]);
    _lines.ShouldAllBe(l => l.ReceivedAt == Now && l.ApiKeyId == ApiKeyId.From(KeyId) && l.Agent == AgentKind.ClaudeCode);
  }

  [Fact]
  public async Task SaysHowManyWereNewAndHowManyAlreadyStored()
  {
    StoreReportsNew(1);

    (await Handle(Command())).Value.ShouldBe(new TranscriptBatchStored(Stored: 1, AlreadyStored: 1));
  }

  [Theory]
  [InlineData("Cursor", "s1", "main", "agent")]
  [InlineData("ClaudeCode", " ", "main", "nativeSessionId")]
  [InlineData("ClaudeCode", "s1", "/home/x.jsonl", "file")]
  public async Task RejectsAnInvalidEnvelope(string agent, string session, string file, string field)
  {
    var result = await Handle(Command(file: file, agent: agent, session: session));

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe(field);
    _lines.ShouldBeNull();
  }

  [Fact]
  public async Task RejectsMetaForTheMainFile()
  {
    // Only subagents have a side file.
    var result = await Handle(Command(file: "main", meta: "{}"));

    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("subagentMeta");
  }

  [Fact]
  public async Task RejectsAnEmptyBatch()
  {
    (await Handle(Command(lines: []))).ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("lines");
  }

  [Fact]
  public async Task RejectsTooManyLines()
  {
    var lines = Enumerable.Range(0, Constants.MAX_TRANSCRIPT_BATCH_LINES + 1).Select(i => new TranscriptLineInput(i * 2, "x")).ToList();

    (await Handle(Command(lines: lines))).ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("lines");
  }

  [Theory]
  [InlineData(-1L, 5L)]
  [InlineData(5L, 5L)]
  public async Task RejectsNegativeOrRepeatedOffsets(long first, long second)
  {
    var result = await Handle(Command(lines: [new(first, "a"), new(second, "b")]));

    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("lines");
    _lines.ShouldBeNull();
  }

  [Fact]
  public async Task AcceptsALineThatIsNotJson()
  {
    // The content of a line never fails a batch (ADR 0020).
    (await Handle(Command(lines: [new(0, "not json {")]))).IsSuccess.ShouldBeTrue();
  }
}
