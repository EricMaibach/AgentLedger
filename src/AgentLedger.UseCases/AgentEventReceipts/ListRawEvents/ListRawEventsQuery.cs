namespace AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

/// <summary>A page of raw events, newest first (ADR 0016).</summary>
/// <param name="Distinct">One row per event (its first receipt) instead of every receipt.</param>
/// <param name="After">Continue after this row; null for the first page.</param>
public sealed record ListRawEventsQuery(RawEventFilter Filter, bool Distinct, RawEventCursor? After, int Limit)
  : IQuery<Result<RawEventPage>>;
