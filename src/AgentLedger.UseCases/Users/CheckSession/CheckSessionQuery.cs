namespace AgentLedger.UseCases.Users.CheckSession;

public sealed record CheckSessionQuery(Guid UserId, Guid SecurityStamp) : IQuery<Result>;
