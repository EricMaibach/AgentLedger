using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.AgentEventReceiptAggregate.Specifications;

namespace AgentLedger.IntegrationTests.Data;

// Checks that EF Core translates the specification to SQL Postgres can run: the filter and
// ordering go through the Vogen value converters on EventId and Id.
[Collection(nameof(PostgresCollection))]
public sealed class ReceiptsByEventIdSpecQuery(PostgresFixture db)
{
  [Fact]
  public async Task ReturnsTheEventsReceiptsOldestFirst()
  {
    var eventId = AgentEventId.From(Guid.CreateVersion7());
    var resend = NewReceipt(eventId, receivedAt: new DateTimeOffset(2026, 10, 1, 9, 0, 30, TimeSpan.Zero));
    var original = NewReceipt(eventId, receivedAt: new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
    var otherEvent = NewReceipt(AgentEventId.From(Guid.CreateVersion7()), receivedAt: new DateTimeOffset(2026, 10, 1, 9, 0, 10, TimeSpan.Zero));

    using (var scope = db.Services.CreateScope())
    {
      var repository = scope.ServiceProvider.GetRequiredService<IRepository<AgentEventReceipt>>();
      await repository.AddRangeAsync([resend, original, otherEvent]);
    }

    using var readScope = db.Services.CreateScope();
    var result = await readScope.ServiceProvider.GetRequiredService<IReadRepository<AgentEventReceipt>>()
      .ListAsync(new ReceiptsByEventIdSpec(eventId));

    result.Select(r => r.Id).ShouldBe([original.Id, resend.Id]);
  }

  private static AgentEventReceipt NewReceipt(AgentEventId eventId, DateTimeOffset receivedAt) =>
    new(ReceiptId.From(Guid.CreateVersion7()), eventId, AgentKind.ClaudeCode, "Stop", "session-1",
      receivedAt.AddMilliseconds(-50), receivedAt,
      new CaptureContext("devbox", "eric", "/workspace", null, null, null),
      new Dictionary<string, string>(), """{"hook_event_name":"Stop"}""");
}
