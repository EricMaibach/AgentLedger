namespace AgentLedger.UseCases.Work.ListTurns;

public sealed record ListTurnsQuery(Guid SessionId) : IQuery<Result<IReadOnlyList<TurnListItem>>>;
