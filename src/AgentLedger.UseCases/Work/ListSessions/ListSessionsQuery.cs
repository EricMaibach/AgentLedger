namespace AgentLedger.UseCases.Work.ListSessions;

public sealed record ListSessionsQuery(SessionCursor? After, int Limit) : IQuery<Result<SessionPage>>;
