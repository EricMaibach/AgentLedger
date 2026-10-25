using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.IntegrationTests.Data;
using AgentLedger.UseCases.Transcripts;

namespace AgentLedger.IntegrationTests.Transcripts;

// Reading transcripts back (ADR 0020). Each test uses its own session.
[Collection(nameof(PostgresCollection))]
public sealed class TranscriptQueryServiceTests(PostgresFixture db)
{
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  [Fact]
  public async Task ListsASessionsFilesMainFirstWithLineCounts()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var early = TranscriptFileName.ForSubagent("early");
    var late = TranscriptFileName.ForSubagent("late");
    await AddAsync(key, late, ["x"], meta: """{"agentType":"Explore"}""", firstSeenMinutes: 2);
    await AddAsync(key, early, ["x", "y"], firstSeenMinutes: 1);
    await AddAsync(key, TranscriptFileName.Main, ["a", "b", "c"], firstSeenMinutes: 3);
    await AddAsync(key, TranscriptFileName.Main, ["a", "b", "c"], firstSeenMinutes: 4, sessionOverride: "another-session");

    var files = await ServiceAsync(s => s.ListFilesAsync(_session, CancellationToken.None));

    files.Select(f => (f.File, f.LineCount)).ShouldBe([("main", 3), ("subagents/agent-early", 2), ("subagents/agent-late", 1)]);
    files[0].Agent.ShouldBe("ClaudeCode");
    files[0].SubagentId.ShouldBeNull();
    files[2].SubagentId.ShouldBe("late");
    files[2].Meta.ShouldBe("""{"agentType":"Explore"}""");
  }

  [Fact]
  public async Task ListsNoFilesForAnUnknownSession()
  {
    (await ServiceAsync(s => s.ListFilesAsync("no-such-session", CancellationToken.None))).ShouldBeEmpty();
  }

  [Fact]
  public async Task PagesThroughAFilesLinesInOrderWithoutGapsOrRepeats()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    await AddAsync(key, TranscriptFileName.Main, ["l0", "l1", "l2", "l3", "l4"]);
    // A rewrite: a second, different line at offset 2 (the second line's offset). Both are kept and both are paged.
    await AddLinesAsync(key, TranscriptFileName.Main, [(2, "l1-rewritten")]);
    await AddAsync(key, TranscriptFileName.ForSubagent("other"), ["not this file"]);

    var seen = new List<string>();
    TranscriptLineCursor? cursor = null;
    var pages = 0;
    do
    {
      var page = await ServiceAsync(s => s.ListLinesAsync(_session, TranscriptFileName.Main, cursor, 2, CancellationToken.None));
      seen.AddRange(page.Items.Select(l => l.Text));
      cursor = page.Next;
      pages++;
    }
    while (cursor is not null && pages < 10);

    seen.Count.ShouldBe(6);
    seen.ShouldBe(["l0", "l1", "l1-rewritten", "l2", "l3", "l4"], ignoreOrder: true);
    seen[0].ShouldBe("l0");
    seen[^1].ShouldBe("l4");
    pages.ShouldBe(3);
  }

  [Fact]
  public async Task ReturnsEachLinesTextAndOffset()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    await AddAsync(key, TranscriptFileName.Main, ["""{"type":"user"}"""]);

    var line = (await ServiceAsync(s => s.ListLinesAsync(_session, TranscriptFileName.Main, null, 10, CancellationToken.None)))
      .Items.ShouldHaveSingleItem();

    line.ByteOffset.ShouldBe(0);
    line.Text.ShouldBe("""{"type":"user"}""");
    line.LineId.ShouldBe(TranscriptLineId.For(AgentKind.ClaudeCode, _session, TranscriptFileName.Main, 0, """{"type":"user"}""").Value);
    line.ReceivedAt.ShouldBe(db.Clock.GetUtcNow());
  }

  // Lines at offsets 0, 2, 4, …: the store only needs distinct offsets.
  private Task AddAsync(ApiKey key, TranscriptFileName file, string[] texts, string? meta = null, int firstSeenMinutes = 0,
    string? sessionOverride = null) =>
    StoreAsync(key, file, texts.Select((t, i) => ((long)i * 2, t)).ToArray(), meta, firstSeenMinutes, sessionOverride);

  private Task AddLinesAsync(ApiKey key, TranscriptFileName file, (long Offset, string Text)[] lines) =>
    StoreAsync(key, file, lines, null, 0, null);

  private async Task StoreAsync(ApiKey key, TranscriptFileName file, (long Offset, string Text)[] lines, string? meta, int firstSeenMinutes,
    string? sessionOverride)
  {
    var session = sessionOverride ?? _session;
    var at = db.Clock.GetUtcNow();
    var transcriptFile = new TranscriptFile(AgentKind.ClaudeCode, session, file, meta, key.Id, at.AddMinutes(firstSeenMinutes));
    var rows = lines.Select(l => new TranscriptLine(AgentKind.ClaudeCode, session, file, l.Offset, l.Text, key.Id, at)).ToList();
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ITranscriptStore>().AddAsync(transcriptFile, rows, CancellationToken.None);
  }

  private async Task<T> ServiceAsync<T>(Func<ITranscriptQueryService, Task<T>> query)
  {
    using var scope = db.Services.CreateScope();
    return await query(scope.ServiceProvider.GetRequiredService<ITranscriptQueryService>());
  }
}
