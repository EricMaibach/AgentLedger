namespace AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

// Every filter is optional, null means "don't filter on this"
public sealed record RawEventFilter(
  string? Agent = null, string? EventType = null, string? NativeSessionId = null,
  string? Host = null, string? GitRepo = null, DateTimeOffset? From = null, DateTimeOffset? To = null);

//Where the previous page ended, the last row's sort key (keyset paging).
public sealed record RawEventCursor(DateTimeOffset ReceivedAt, Guid ReceiptId);

// One row of the list.
public sealed record RawEventListItem(
  Guid ReceiptId, Guid EventId, string Agent, string EventType, string? NativeSessionId,
  DateTimeOffset CapturedAt, DateTimeOffset ReceivedAt, string Host, string User,
  string? GitRepo, string? GitBranch, int ReceiptCount, string Preview)
{
  public const int PreviewLength = 100;
}

// One page of rows.  Next is null on the last page.
public sealed record RawEventPage(IReadOnlyList<RawEventListItem> Items, RawEventCursor? Next);

// For a session's "PreToolUse: 42, Stop: 12" summary.
public sealed record EventTypeCount(string EventType, int Count);
