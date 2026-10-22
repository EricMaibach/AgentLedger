namespace AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

public interface IRawEventQueryService
{
  Task<RawEventPage> ListAsync(RawEventFilter filter, bool distinct, RawEventCursor? after, int limit, CancellationToken cancellationToken);
  Task<IReadOnlyList<EventTypeCount>> CountEventTypesAsync(RawEventFilter filter, CancellationToken cancellationToken);
}
