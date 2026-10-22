namespace AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

/// <summary>How many events of each type match the filter (e.g. for one session).</summary>
public sealed record CountRawEventTypesQuery(RawEventFilter Filter) : IQuery<Result<IReadOnlyList<EventTypeCount>>>;
