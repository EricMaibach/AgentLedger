using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.IntegrationTests.Data;
using AgentLedger.UseCases.Transcripts;

namespace AgentLedger.IntegrationTests.Transcripts;

// Reading transcript lines back like raw events (ADR 0020, revised). Each test uses its own session.
[Collection(nameof(PostgresCollection))]
public sealed class TranscriptQueryServiceTests(PostgresFixture db)
{
  private static readonly DateTimeOffset T0 = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

  private readonly string _session = $"session-{Guid.NewGuid():N}";

  [Fact]
  public async Task ListsNewestFirstKeepingABatchInFileOrder()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    await StoreAsync(key, TranscriptFileName.Main, T0, [(0, "a"), (10, "b"), (20, "c")]); // one batch, one instant
    await StoreAsync(key, TranscriptFileName.Main, T0.AddSeconds(5), [(30, "d")]);

    var page = await ListAsync(new TranscriptLineFilter(NativeSessionId: _session));

    page.Items.Select(i => i.ByteOffset).ShouldBe([30L, 20L, 10L, 0L]);
    page.Next.ShouldBeNull();
  }

  [Fact]
  public async Task PagesThroughEverythingWithoutGapsOrRepeats()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    await StoreAsync(key, TranscriptFileName.Main, T0, [(0, "l0"), (2, "l1"), (4, "l2")]);
    await StoreAsync(key, TranscriptFileName.Main, T0, [(2, "l1-rewritten")]); // same instant and offset: the ID breaks the tie
    await StoreAsync(key, TranscriptFileName.Main, T0.AddSeconds(1), [(6, "l3"), (8, "l4")]);

    var seen = new List<string>();
    TranscriptLineCursor? cursor = null;
    var pages = 0;
    do
    {
      var page = await ListAsync(new TranscriptLineFilter(NativeSessionId: _session), cursor, limit: 2);
      seen.AddRange(page.Items.Select(i => i.Preview));
      cursor = page.Next;
      pages++;
    }
    while (cursor is not null && pages < 10);

    seen.Count.ShouldBe(6);
    seen.ShouldBe(["l4", "l3", "l2", "l1", "l1-rewritten", "l0"], ignoreOrder: true);
    seen.Take(3).ShouldBe(["l4", "l3", "l2"]);
    seen[^1].ShouldBe("l0");
    pages.ShouldBe(3);
  }

  [Fact]
  public async Task FiltersByAgentLineTypeSessionFileAndReceivedTime()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var subagent = TranscriptFileName.ForSubagent("a1");
    await StoreAsync(key, TranscriptFileName.Main, T0, [(0, """{"type":"user"}"""), (20, """{"type":"assistant"}""")]);
    await StoreAsync(key, subagent, T0, [(0, """{"type":"assistant"}""")]);
    await StoreAsync(key, TranscriptFileName.Main, T0.AddMinutes(10), [(40, """{"type":"assistant"}""")]);

    var page = await ListAsync(new TranscriptLineFilter(Agent: "ClaudeCode", LineType: "assistant", NativeSessionId: _session,
      File: "main", From: T0, To: T0.AddMinutes(10)));

    page.Items.ShouldHaveSingleItem().ByteOffset.ShouldBe(20);
  }

  [Fact]
  public async Task FillsInEveryColumnWithAShortPreview()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var text = "{\"type\":\"assistant\",\"message\":{\"content\":\"" + new string('x', 300) + "\"}}";
    await StoreAsync(key, TranscriptFileName.ForSubagent("a1"), T0, [(412, text)]);

    var item = (await ListAsync(new TranscriptLineFilter(NativeSessionId: _session))).Items.ShouldHaveSingleItem();

    item.LineId.ShouldBe(TranscriptLineId.For(AgentKind.ClaudeCode, _session, TranscriptFileName.ForSubagent("a1"), 412, text).Value);
    item.Agent.ShouldBe("ClaudeCode");
    item.LineType.ShouldBe("assistant");
    item.NativeSessionId.ShouldBe(_session);
    item.File.ShouldBe("subagents/agent-a1");
    item.SubagentId.ShouldBe("a1");
    item.ByteOffset.ShouldBe(412);
    item.ReceivedAt.ShouldBe(T0);
    item.Preview.ShouldBe(text[..TranscriptLineListItem.PreviewLength]);
  }

  [Fact]
  public async Task GetsOneLineWithItsFilesMeta()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var subagent = TranscriptFileName.ForSubagent("a1");
    await StoreAsync(key, subagent, T0, [(7, """{"type":"user"}""")], meta: """{"agentType":"Explore"}""");
    var lineId = TranscriptLineId.For(AgentKind.ClaudeCode, _session, subagent, 7, """{"type":"user"}""");

    var row = await ServiceAsync(s => s.GetLineAsync(lineId, CancellationToken.None));

    row.ShouldBe(new TranscriptLineRow(lineId.Value, "ClaudeCode", "user", _session, "subagents/agent-a1", "a1", 7, """{"type":"user"}""",
      T0, key.Id.Value, """{"agentType":"Explore"}"""));
  }

  [Fact]
  public async Task GetsNothingForAnUnknownLine()
  {
    (await ServiceAsync(s => s.GetLineAsync(TranscriptLineId.From(Guid.CreateVersion7()), CancellationToken.None))).ShouldBeNull();
  }

  private Task<TranscriptLinePage> ListAsync(TranscriptLineFilter filter, TranscriptLineCursor? after = null, int limit = 50) =>
    ServiceAsync(s => s.ListLinesAsync(filter, after, limit, CancellationToken.None));

  private async Task StoreAsync(ApiKey key, TranscriptFileName file, DateTimeOffset receivedAt, (long Offset, string Text)[] lines, string? meta = null)
  {
    var transcriptFile = new TranscriptFile(AgentKind.ClaudeCode, _session, file, meta, key.Id, receivedAt);
    var rows = lines.Select(l => new TranscriptLine(AgentKind.ClaudeCode, _session, file, l.Offset, l.Text, key.Id, receivedAt)).ToList();
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ITranscriptStore>().AddAsync(transcriptFile, rows, CancellationToken.None);
  }

  private async Task<T> ServiceAsync<T>(Func<ITranscriptQueryService, Task<T>> query)
  {
    using var scope = db.Services.CreateScope();
    return await query(scope.ServiceProvider.GetRequiredService<ITranscriptQueryService>());
  }
}
