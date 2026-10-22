using AgentLedger.UseCases;

namespace AgentLedger.Web.AgentEventReceipts;

/// <summary>Query parameters for GET /api/raw/events (ADR 0016, 0017). Every filter is optional.</summary>
public sealed class ListRawEventsRequest
{
  public const string Route = "/api/raw/events"; // pages own /raw/events (ADR 0017)

  /// <summary>Kebab-case agent name, e.g. <c>claude-code</c>.</summary>
  public string? Agent { get; set; }
  public string? EventType { get; set; }

  /// <summary>The agent's native session ID.</summary>
  public string? Session { get; set; }
  public string? Host { get; set; }

  /// <summary>Normalized git repository, e.g. <c>github.com/owner/repo</c>.</summary>
  public string? Repo { get; set; }

  /// <summary>Received at or after (inclusive).</summary>
  public DateTimeOffset? From { get; set; }

  /// <summary>Received before (exclusive).</summary>
  public DateTimeOffset? To { get; set; }

  /// <summary>One row per event (its first receipt) instead of every receipt. Default true.</summary>
  public bool Distinct { get; set; } = true;

  public int Limit { get; set; } = Constants.DEFAULT_PAGE_SIZE;

  /// <summary>The <c>nextCursor</c> from the previous page; omit for the first page.</summary>
  public string? After { get; set; }
}

/// <param name="Items">The rows, newest first. <c>agent</c> uses the wire name.</param>
/// <param name="NextCursor">Pass as <c>after</c> to get the next (older) page; null on the last page.</param>
public sealed record ListRawEventsResponse(IReadOnlyList<UseCases.AgentEventReceipts.ListRawEvents.RawEventListItem> Items, string? NextCursor);
