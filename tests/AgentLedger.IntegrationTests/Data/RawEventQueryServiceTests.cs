using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

namespace AgentLedger.IntegrationTests.Data;

// The raw event list's read model (ADR 0016): keyset paging, one row per event, filters and counts,
// run against real Postgres. Every test uses its own session ID (or host) and filters on it, so tests
// sharing the database don't see each other's rows.
[Collection(nameof(PostgresCollection))]
public sealed class RawEventQueryServiceTests(PostgresFixture db)
{
  private static readonly DateTimeOffset T0 = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

  private readonly string _session = $"session-{Guid.NewGuid():N}";

  [Fact]
  public async Task ListsNewestFirst()
  {
    var older = NewReceipt(receivedSeconds: 0);
    var newer = NewReceipt(receivedSeconds: 10);
    await SaveAsync(older, newer);

    var page = await ListAsync(new RawEventFilter(NativeSessionId: _session));

    page.Items.Select(i => i.ReceiptId).ShouldBe([newer.Id.Value, older.Id.Value]);
    page.Next.ShouldBeNull(); // everything fitted on one page
  }

  [Fact]
  public async Task PagesThroughEverythingWithoutGapsOrRepeats()
  {
    // Seven receipts, four of them in the same instant: page boundaries fall inside the tie,
    // which is exactly where ordering by time alone would skip or repeat rows.
    var receipts = new[]
    {
      NewReceipt(receivedSeconds: 0), NewReceipt(receivedSeconds: 5), NewReceipt(receivedSeconds: 5),
      NewReceipt(receivedSeconds: 5), NewReceipt(receivedSeconds: 5), NewReceipt(receivedSeconds: 9),
      NewReceipt(receivedSeconds: 12),
    };
    await SaveAsync(receipts);
    var filter = new RawEventFilter(NativeSessionId: _session);

    var seen = new List<Guid>();
    RawEventCursor? cursor = null;
    var pages = 0;
    do
    {
      var page = await ListAsync(filter, after: cursor, limit: 3);
      seen.AddRange(page.Items.Select(i => i.ReceiptId));
      cursor = page.Next;
      pages++;
    }
    while (cursor is not null && pages < 10);

    var expected = receipts
      .OrderByDescending(r => r.ReceivedAt).ThenByDescending(r => r.Id)
      .Select(r => r.Id.Value);
    seen.ShouldBe(expected);
    pages.ShouldBe(3); // 3 + 3 + 1
  }

  [Fact]
  public async Task ShowsOneRowPerEventByDefault()
  {
    // A resent event appears once, as its first receipt, with the number of receipts it has.
    var eventId = AgentEventId.From(Guid.CreateVersion7());
    var original = NewReceipt(receivedSeconds: 0, eventId: eventId);
    var resend1 = NewReceipt(receivedSeconds: 3, eventId: eventId);
    var resend2 = NewReceipt(receivedSeconds: 7, eventId: eventId);
    var other = NewReceipt(receivedSeconds: 5);
    await SaveAsync(original, resend1, resend2, other);

    var page = await ListAsync(new RawEventFilter(NativeSessionId: _session));

    page.Items.Select(i => i.ReceiptId).ShouldBe([other.Id.Value, original.Id.Value]);
    page.Items.Single(i => i.EventId == eventId.Value).ReceiptCount.ShouldBe(3);
    page.Items.Single(i => i.EventId == other.EventId.Value).ReceiptCount.ShouldBe(1);
  }

  [Fact]
  public async Task ShowsEveryReceiptWhenNotDistinct()
  {
    var eventId = AgentEventId.From(Guid.CreateVersion7());
    await SaveAsync(NewReceipt(receivedSeconds: 0, eventId: eventId), NewReceipt(receivedSeconds: 3, eventId: eventId));

    var page = await ListAsync(new RawEventFilter(NativeSessionId: _session), distinct: false);

    page.Items.Count.ShouldBe(2);
    page.Items.ShouldAllBe(i => i.ReceiptCount == 2);
  }

  [Fact]
  public async Task FillsInEveryColumn()
  {
    var receipt = NewReceipt(receivedSeconds: 0, eventType: "UserPromptSubmit",
      payload: """{"session_id":"s","prompt":"Explain keyset paging, please, in some detail so the preview gets cut off well before the end."}""");
    await SaveAsync(receipt);

    var item = (await ListAsync(new RawEventFilter(NativeSessionId: _session))).Items.ShouldHaveSingleItem();

    item.ReceiptId.ShouldBe(receipt.Id.Value);
    item.EventId.ShouldBe(receipt.EventId.Value);
    item.Agent.ShouldBe("ClaudeCode");
    item.EventType.ShouldBe("UserPromptSubmit");
    item.NativeSessionId.ShouldBe(_session);
    item.CapturedAt.ShouldBe(receipt.CapturedAt);
    item.ReceivedAt.ShouldBe(receipt.ReceivedAt);
    item.Host.ShouldBe("devbox");
    item.User.ShouldBe("eric");
    item.GitRepo.ShouldBe("github.com/owner/repo");
    item.GitBranch.ShouldBe("main");
    item.Preview.ShouldBe(receipt.Payload[..RawEventListItem.PreviewLength]); // raw text, cut to length
  }

  [Fact]
  public async Task FiltersByAgentEventTypeHostAndRepo()
  {
    var match = NewReceipt(receivedSeconds: 0, eventType: "Stop");
    await SaveAsync(
      match,
      NewReceipt(receivedSeconds: 1, eventType: "PreToolUse"),                    // other event type
      NewReceipt(receivedSeconds: 2, eventType: "Stop", agent: AgentKind.Codex),  // other agent
      NewReceipt(receivedSeconds: 3, eventType: "Stop", host: "other-host"),      // other host
      NewReceipt(receivedSeconds: 4, eventType: "Stop", gitRepo: "github.com/owner/other"));

    var page = await ListAsync(new RawEventFilter(
      Agent: "ClaudeCode", EventType: "Stop", NativeSessionId: _session, Host: "devbox", GitRepo: "github.com/owner/repo"));

    page.Items.ShouldHaveSingleItem().ReceiptId.ShouldBe(match.Id.Value);
  }

  [Fact]
  public async Task FiltersByReceivedTimeFromInclusiveToExclusive()
  {
    var before = NewReceipt(receivedSeconds: 0);
    var atFrom = NewReceipt(receivedSeconds: 10);
    var inside = NewReceipt(receivedSeconds: 15);
    var atTo = NewReceipt(receivedSeconds: 20);
    await SaveAsync(before, atFrom, inside, atTo);

    var page = await ListAsync(new RawEventFilter(NativeSessionId: _session, From: T0.AddSeconds(10), To: T0.AddSeconds(20)));

    page.Items.Select(i => i.ReceiptId).ShouldBe([inside.Id.Value, atFrom.Id.Value]);
  }

  [Fact]
  public async Task CountsEventsByTypeForASession()
  {
    // Counts events, not receipts: a resend doesn't make an event count twice.
    var resent = AgentEventId.From(Guid.CreateVersion7());
    await SaveAsync(
      NewReceipt(receivedSeconds: 0, eventType: "PreToolUse", eventId: resent),
      NewReceipt(receivedSeconds: 1, eventType: "PreToolUse", eventId: resent),
      NewReceipt(receivedSeconds: 2, eventType: "PreToolUse"),
      NewReceipt(receivedSeconds: 3, eventType: "Stop"));

    var counts = await ServiceAsync(service => service.CountEventTypesAsync(new RawEventFilter(NativeSessionId: _session), CancellationToken.None));

    counts.ShouldBe([new EventTypeCount("PreToolUse", 2), new EventTypeCount("Stop", 1)], ignoreOrder: true);
  }

  private Task<RawEventPage> ListAsync(RawEventFilter filter, bool distinct = true, RawEventCursor? after = null, int limit = 50) =>
    ServiceAsync(service => service.ListAsync(filter, distinct, after, limit, CancellationToken.None));

  private async Task<T> ServiceAsync<T>(Func<IRawEventQueryService, Task<T>> query)
  {
    using var scope = db.Services.CreateScope();
    return await query(scope.ServiceProvider.GetRequiredService<IRawEventQueryService>());
  }

  private async Task SaveAsync(params AgentEventReceipt[] receipts)
  {
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<AgentEventReceipt>>().AddRangeAsync(receipts);
  }

  private AgentEventReceipt NewReceipt(
    int receivedSeconds,
    AgentEventId? eventId = null,
    string eventType = "PreToolUse",
    AgentKind? agent = null,
    string host = "devbox",
    string gitRepo = "github.com/owner/repo",
    string payload = """{"hook_event_name":"PreToolUse"}""") =>
    new(
      ReceiptId.From(Guid.CreateVersion7()),
      eventId ?? AgentEventId.From(Guid.CreateVersion7()),
      agent ?? AgentKind.ClaudeCode,
      eventType,
      _session,
      T0.AddSeconds(receivedSeconds).AddMilliseconds(-40),
      T0.AddSeconds(receivedSeconds),
      new CaptureContext(host, "eric", "/workspace", gitRepo, "main", null),
      new Dictionary<string, string>(),
      payload);
}
