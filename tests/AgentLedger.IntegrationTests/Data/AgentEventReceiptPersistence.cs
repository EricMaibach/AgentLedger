using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Infrastructure.Data;

namespace AgentLedger.IntegrationTests.Data;

[Collection(nameof(PostgresCollection))]
public sealed class AgentEventReceiptPersistence(PostgresFixture db)
{
  [Fact]
  public async Task RoundTripsEveryProperty()
  {
    // Whitespace, key order and a duplicate key: jsonb would rewrite all three (ADR 0009).
    const string payload = "{ \"z\": 1,\n  \"a\": [true, null],  \"a\": \"duplicate key\" }";
    var original = NewReceipt(payload: payload);

    await SaveAsync(original);
    var loaded = await LoadAsync(original.Id);

    loaded.Id.ShouldBe(original.Id);
    loaded.EventId.ShouldBe(original.EventId);
    loaded.Agent.ShouldBe(original.Agent);
    loaded.EventType.ShouldBe(original.EventType);
    loaded.NativeSessionId.ShouldBe(original.NativeSessionId);
    loaded.CapturedAt.ShouldBe(original.CapturedAt);
    loaded.ReceivedAt.ShouldBe(original.ReceivedAt);
    loaded.Context.ShouldBe(original.Context);
    loaded.Tags.ShouldBe(original.Tags, ignoreOrder: true);
    loaded.Payload.ShouldBe(payload);
  }

  [Fact]
  public async Task RoundTripsOptionalValuesAsNull()
  {
    var original = NewReceipt(nativeSessionId: null, context: new CaptureContext("devbox", "eric", "/tmp/scratch", null, null, null));

    await SaveAsync(original);
    var loaded = await LoadAsync(original.Id);

    loaded.NativeSessionId.ShouldBeNull();
    loaded.Context.ShouldBe(original.Context);
  }

  [Fact]
  public async Task StoresEveryReceiptOfTheSameEvent()
  {
    // ELT: a resend is landed as another receipt, not rejected (ADR 0010).
    var eventId = AgentEventId.From(Guid.CreateVersion7());

    await SaveAsync(NewReceipt(eventId: eventId));
    await SaveAsync(NewReceipt(eventId: eventId));

    using var scope = db.Services.CreateScope();
    var count = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
      .AgentEventReceipts.CountAsync(r => r.EventId == eventId);
    count.ShouldBe(2);
  }

  [Fact]
  public async Task SetsAuditTimestampsOnInsert()
  {
    var receipt = NewReceipt();

    await SaveAsync(receipt);

    using var scope = db.Services.CreateScope();
    var timestamps = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
      .AgentEventReceipts
      .Where(r => r.Id == receipt.Id)
      .Select(r => new
      {
        CreatedAt = EF.Property<DateTimeOffset>(r, AppDbContext.CreatedAt),
        UpdatedAt = EF.Property<DateTimeOffset>(r, AppDbContext.UpdatedAt)
      })
      .SingleAsync();

    timestamps.CreatedAt.ShouldBe(db.Clock.GetUtcNow());
    timestamps.UpdatedAt.ShouldBe(db.Clock.GetUtcNow());
  }

  [Fact]
  public async Task RejectsDuplicateReceiptId()
  {
    var first = NewReceipt();
    await SaveAsync(first);

    await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(NewReceipt(receiptId: first.Id)));
  }

  private async Task SaveAsync(AgentEventReceipt receipt)
  {
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<AgentEventReceipt>>().AddAsync(receipt);
  }

  // A fresh scope means a fresh DbContext, so the receipt really comes from the database,
  // not from the change tracker that saved it.
  private async Task<AgentEventReceipt> LoadAsync(ReceiptId id)
  {
    using var scope = db.Services.CreateScope();
    var loaded = await scope.ServiceProvider.GetRequiredService<IReadRepository<AgentEventReceipt>>().GetByIdAsync(id);
    return loaded.ShouldNotBeNull();
  }

  private static AgentEventReceipt NewReceipt(
    ReceiptId? receiptId = null,
    AgentEventId? eventId = null,
    string? nativeSessionId = "3829bce8-0000-0000-0000-000000000000",
    CaptureContext? context = null,
    string payload = """{"session_id":"3829bce8","hook_event_name":"PreToolUse"}""") =>
    new(
      receiptId ?? ReceiptId.From(Guid.CreateVersion7()),
      eventId ?? AgentEventId.From(Guid.CreateVersion7()),
      AgentKind.ClaudeCode,
      "PreToolUse",
      nativeSessionId,
      new DateTimeOffset(2026, 9, 23, 1, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 9, 23, 1, 0, 0, 40, TimeSpan.Zero),
      context ?? new CaptureContext("devbox", "eric", "/workspace", "github.com/EricMaibach/AgentLedger", "main", "/workspace"),
      new Dictionary<string, string> { ["story"] = "123", ["phase"] = "build" },
      payload);
}
