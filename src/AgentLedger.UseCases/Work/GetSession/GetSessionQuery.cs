namespace AgentLedger.UseCases.Work.GetSession;

public sealed record GetSessionQuery(Guid SessionId) : IQuery<Result<SessionDetail>>;
