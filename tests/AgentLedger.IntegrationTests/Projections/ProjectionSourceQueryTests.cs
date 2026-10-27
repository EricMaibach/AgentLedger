using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.IntegrationTests.Data;
using AgentLedger.UseCases.Projections;
using AgentLedger.UseCases.Transcripts;

namespace AgentLedger.IntegrationTests.Projections;

// Reading raw rows for projections (ADR 0022) against real Postgres.
[Collection(nameof(PostgresCollection))]
public sealed class ProjectionSourceQueryTests(PostgresFixture db)
{
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  [Fact]
  public async Task ReadsASessionsReceiptsAndLines()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var first = await SaveReceiptAsync(_session);
    await SaveReceiptAsync($"other-{Guid.NewGuid():N}");
    var resend = await SaveReceiptAsync(_session, first.EventId);
    await SaveLinesAsync(key, TranscriptFileName.Main, (20, "b"), (0, "a"));
    await SaveLinesAsync(key, TranscriptFileName.ForSubagent("a1"), (0, "c"));

    var evidence = await Query().ReadSessionAsync(Key, CancellationToken.None);

    evidence.ShouldNotBeNull();
    (evidence.Agent, evidence.NativeSessionId).ShouldBe((AgentKind.ClaudeCode, _session));
    evidence.Receipts.Select(r => r.Id).ShouldBe([first.Id, resend.Id], ignoreOrder: true);
    evidence.Lines.Select(l => (l.File.Value, l.Text)).ShouldBe([("main", "a"), ("main", "b"), ("subagents/agent-a1", "c")]);
  }

  [Fact]
  public async Task ReadsNothingForAnUnknownSession()
  {
    (await Query().ReadSessionAsync(Key, CancellationToken.None)).ShouldBeNull();
  }

  [Fact]
  public async Task FindsSessionsChangedWithinTheWindow()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var before = db.Clock.GetUtcNow();
    db.Clock.Advance(TimeSpan.FromSeconds(10));
    var withEvent = $"session-{Guid.NewGuid():N}";
    await SaveReceiptAsync(withEvent);
    await SaveLinesAsync(key, TranscriptFileName.Main, (0, "a")); // _session, lines only
    var stored = db.Clock.GetUtcNow();
    db.Clock.Advance(TimeSpan.FromSeconds(10));

    var changed = await Query().FindChangedSessionsAsync(before, stored, CancellationToken.None);
    var all = await Query().FindChangedSessionsAsync(null, stored, CancellationToken.None);
    var later = await Query().FindChangedSessionsAsync(stored, db.Clock.GetUtcNow(), CancellationToken.None);
    var earlier = await Query().FindChangedSessionsAsync(null, before, CancellationToken.None);

    changed.ShouldContain(Key);
    changed.ShouldContain(new SessionKey(AgentKind.ClaudeCode, withEvent));
    changed.Count(k => k == Key).ShouldBe(1);
    all.ShouldContain(Key);
    later.ShouldNotContain(Key);
    earlier.ShouldNotContain(Key);
  }

  private SessionKey Key => new(AgentKind.ClaudeCode, _session);

  private IProjectionSourceQuery Query() =>
    db.Services.CreateScope().ServiceProvider.GetRequiredService<IProjectionSourceQuery>();

  private async Task<AgentEventReceipt> SaveReceiptAsync(string session, AgentEventId? eventId = null)
  {
    var receipt = new AgentEventReceipt(ReceiptId.From(Guid.CreateVersion7()), eventId ?? AgentEventId.From(Guid.CreateVersion7()),
      AgentKind.ClaudeCode, "Stop", session, db.Clock.GetUtcNow(), db.Clock.GetUtcNow(),
      new CaptureContext("devbox", "eric", "/w", null, null, null), new Dictionary<string, string>(), "{}");
    using var scope = db.Services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IRepository<AgentEventReceipt>>();
    await repository.AddAsync(receipt);
    return receipt;
  }

  private async Task SaveLinesAsync(ApiKey key, TranscriptFileName file, params (long Offset, string Text)[] lines)
  {
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ITranscriptStore>().AddAsync(
      new TranscriptFile(AgentKind.ClaudeCode, _session, file, null, key.Id, db.Clock.GetUtcNow()),
      [.. lines.Select(l => new TranscriptLine(AgentKind.ClaudeCode, _session, file, l.Offset, l.Text, key.Id, db.Clock.GetUtcNow()))],
      CancellationToken.None);
  }
}
