using AgentLedger.Core.AgentEventAggregate;
using AgentLedger.Infrastructure.Data;

namespace AgentLedger.IntegrationTests.Data;

[Collection(nameof(PostgresCollection))]
public sealed class AgentEventPersistence(PostgresFixture db)
{
  [Fact]
  public async Task RoundTripsEveryProperty()
  {
    // Whitespace, key order and a duplicate key: jsonb would rewrite all three (ADR 0009).
    const string payload = "{ \"z\": 1,\n  \"a\": [true, null],  \"a\": \"duplicate key\" }";
    var original = NewEvent(payload: payload);

    await SaveAsync(original);
    var loaded = await LoadAsync(original.Id);

    loaded.Id.ShouldBe(original.Id);
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
    var original = NewEvent(nativeSessionId: null, context: new CaptureContext("devbox", "eric", "/tmp/scratch", null, null, null));

    await SaveAsync(original);
    var loaded = await LoadAsync(original.Id);

    loaded.NativeSessionId.ShouldBeNull();
    loaded.Context.ShouldBe(original.Context);
  }

  [Fact]
  public async Task SetsAuditTimestampsOnInsert()
  {
    var agentEvent = NewEvent();

    await SaveAsync(agentEvent);

    using var scope = db.Services.CreateScope();
    var timestamps = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
      .Set<AgentEvent>()
      .Where(e => e.Id == agentEvent.Id)
      .Select(e => new
      {
        CreatedAt = EF.Property<DateTimeOffset>(e, "CreatedAt"),
        UpdatedAt = EF.Property<DateTimeOffset>(e, "UpdatedAt")
      })
      .SingleAsync();

    timestamps.CreatedAt.ShouldBe(db.Clock.GetUtcNow());
    timestamps.UpdatedAt.ShouldBe(db.Clock.GetUtcNow());
  }

  [Fact]
  public async Task RejectsDuplicateId()
  {
    var first = NewEvent();
    await SaveAsync(first);

    await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(NewEvent(id: first.Id)));
  }

  private async Task SaveAsync(AgentEvent agentEvent)
  {
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<AgentEvent>>().AddAsync(agentEvent);
  }

  // A fresh scope means a fresh DbContext, so the event really comes from the database,
  // not from the change tracker that saved it.
  private async Task<AgentEvent> LoadAsync(AgentEventId id)
  {
    using var scope = db.Services.CreateScope();
    var loaded = await scope.ServiceProvider.GetRequiredService<IReadRepository<AgentEvent>>().GetByIdAsync(id);
    return loaded.ShouldNotBeNull();
  }

  private static AgentEvent NewEvent(
    AgentEventId? id = null,
    string? nativeSessionId = "3829bce8-0000-0000-0000-000000000000",
    CaptureContext? context = null,
    string payload = """{"session_id":"3829bce8","hook_event_name":"PreToolUse"}""") =>
    new(
      id ?? AgentEventId.From(Guid.CreateVersion7()),
      AgentKind.ClaudeCode,
      "PreToolUse",
      nativeSessionId,
      new DateTimeOffset(2026, 9, 23, 1, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 9, 23, 1, 0, 0, 40, TimeSpan.Zero),
      context ?? new CaptureContext("devbox", "eric", "/workspace", "github.com/EricMaibach/AgentLedger", "main", "/workspace"),
      new Dictionary<string, string> { ["story"] = "123", ["phase"] = "build" },
      payload);
}
