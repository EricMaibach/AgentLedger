namespace AgentLedger.UseCases.AgentEventReceipts.GetRawEvent;

public sealed record GetRawEventQuery(Guid EventId) : IQuery<Result<RawEventDetail>>;
