using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.Infrastructure.Data;
using AgentLedger.IntegrationTests.Data;
using AgentLedger.UseCases.Transcripts;

namespace AgentLedger.IntegrationTests.Transcripts;

// Storing transcript batches (ADR 0020) against real Postgres. Each test uses its own session.
[Collection(nameof(PostgresCollection))]
public sealed class TranscriptStoreTests(PostgresFixture db)
{
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  [Fact]
  public async Task StoresNewLinesWithEveryValue()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var lines = new[] { Line(key, 0, """{"type":"user"}"""), Line(key, 16, """{"type":"assistant"}""") };

    var stored = await AddAsync(File(key), lines);

    stored.ShouldBe(2);
    var loaded = await LoadLinesAsync();
    loaded.Select(l => l.Id).ShouldBe(lines.Select(l => l.Id));
    var first = loaded[0];
    first.Agent.ShouldBe(AgentKind.ClaudeCode);
    first.NativeSessionId.ShouldBe(_session);
    first.File.ShouldBe(TranscriptFileName.Main);
    first.SubagentId.ShouldBeNull();
    first.ByteOffset.ShouldBe(0);
    first.Text.ShouldBe("""{"type":"user"}""");
    first.LineType.ShouldBe("user");
    first.ApiKeyId.ShouldBe(key.Id);
    first.ReceivedAt.ShouldBe(db.Clock.GetUtcNow());
  }

  [Fact]
  public async Task SkipsLinesAlreadyStoredAndKeepsTheFirstArrival()
  {
    var firstKey = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var secondKey = await ApiKeyPersistence.SaveNewKeyAsync(db);
    await AddAsync(File(firstKey), [Line(firstKey, 0, "a"), Line(firstKey, 2, "b")]);

    // A resend that overlaps: two lines already stored (sent with another key), one new.
    var stored = await AddAsync(File(secondKey), [Line(secondKey, 0, "a"), Line(secondKey, 2, "b"), Line(secondKey, 4, "c")]);

    stored.ShouldBe(1);
    var loaded = await LoadLinesAsync();
    loaded.Select(l => l.Text).ShouldBe(["a", "b", "c"]);
    loaded[0].ApiKeyId.ShouldBe(firstKey.Id);
  }

  [Fact]
  public async Task KeepsADifferentLineAtTheSameOffset()
  {
    // A rewritten file: same offset, different bytes, so a different line (ADR 0020).
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    await AddAsync(File(key), [Line(key, 0, "original")]);

    (await AddAsync(File(key), [Line(key, 0, "rewritten")])).ShouldBe(1);

    (await LoadLinesAsync()).Select(l => l.Text).ShouldBe(["original", "rewritten"], ignoreOrder: true);
  }

  [Theory]
  [InlineData("{\"type\":\"user\",  not json")]
  [InlineData("")]
  [InlineData("{\"text\":\"naïve 🔑 \\u00e9\"}")]
  public async Task KeepsTheTextExactly(string text)
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);

    await AddAsync(File(key), [Line(key, 0, text)]);

    (await LoadLinesAsync()).ShouldHaveSingleItem().Text.ShouldBe(text);
  }

  [Fact]
  public async Task StoresALargeBatchInOneGo()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var big = new string('x', 120_000); // tool results with file contents reach about 115 KB
    var lines = Enumerable.Range(0, 1000).Select(i => Line(key, i * 200_000L, i == 0 ? big : $"line {i}")).ToList();

    (await AddAsync(File(key), lines)).ShouldBe(1000);

    var loaded = await LoadLinesAsync();
    loaded.Count.ShouldBe(1000);
    loaded[0].Text.ShouldBe(big);
  }

  [Fact]
  public async Task RecordsAFileOnceAndFillsInItsMetaTheFirstTimeItArrives()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var subagent = TranscriptFileName.ForSubagent("a1b2c3");
    await AddAsync(File(key, subagent, meta: null), [Line(key, 0, "a", subagent)]);

    await AddAsync(File(key, subagent, meta: """{"agentType":"Explore"}"""), [Line(key, 2, "b", subagent)]);
    await AddAsync(File(key, subagent, meta: """{"agentType":"changed"}"""), [Line(key, 4, "c", subagent)]);

    var file = (await LoadFilesAsync()).ShouldHaveSingleItem();
    file.File.ShouldBe(subagent);
    file.SubagentId.ShouldBe("a1b2c3");
    file.Meta.ShouldBe("""{"agentType":"Explore"}""");
  }

  [Fact]
  public async Task SetsAuditTimestamps()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    await AddAsync(File(key), [Line(key, 0, "a")]);

    using var scope = db.Services.CreateScope();
    var created = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<TranscriptLine>()
      .Where(l => l.NativeSessionId == _session)
      .Select(l => EF.Property<DateTimeOffset>(l, AppDbContext.CreatedAt))
      .SingleAsync();
    created.ShouldBe(db.Clock.GetUtcNow());
  }

  [Fact]
  public async Task RefusesLinesOfAnotherFile()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);

    await Assert.ThrowsAnyAsync<ArgumentException>(() =>
      AddAsync(File(key), [Line(key, 0, "a", TranscriptFileName.ForSubagent("a1b2c3"))]));
    (await LoadLinesAsync()).ShouldBeEmpty();
  }

  [Fact]
  public async Task RequiresAKeyThatExists()
  {
    var missing = new ApiKey(ApiKeyId.From(Guid.CreateVersion7()), Core.UserAggregate.UserId.From(Guid.CreateVersion7()), "missing",
      ApiKeyScope.Ingest, ApiKeySecretHash.Of("x"), db.Clock.GetUtcNow(), null);

    await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => AddAsync(File(missing), [Line(missing, 0, "a")]));
  }

  private TranscriptFile File(ApiKey key, TranscriptFileName? name = null, string? meta = null) =>
    new(AgentKind.ClaudeCode, _session, name ?? TranscriptFileName.Main, meta, key.Id, db.Clock.GetUtcNow());

  private TranscriptLine Line(ApiKey key, long offset, string text, TranscriptFileName? name = null) =>
    new(AgentKind.ClaudeCode, _session, name ?? TranscriptFileName.Main, offset, text, key.Id, db.Clock.GetUtcNow());

  private async Task<int> AddAsync(TranscriptFile file, IReadOnlyList<TranscriptLine> lines)
  {
    using var scope = db.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<ITranscriptStore>().AddAsync(file, lines, CancellationToken.None);
  }

  private async Task<List<TranscriptLine>> LoadLinesAsync()
  {
    using var scope = db.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<TranscriptLine>().AsNoTracking()
      .Where(l => l.NativeSessionId == _session).OrderBy(l => l.File).ThenBy(l => l.ByteOffset).ToListAsync();
  }

  private async Task<List<TranscriptFile>> LoadFilesAsync()
  {
    using var scope = db.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<TranscriptFile>().AsNoTracking()
      .Where(f => f.NativeSessionId == _session).ToListAsync();
  }
}
