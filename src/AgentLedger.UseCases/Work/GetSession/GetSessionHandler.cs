using AgentLedger.UseCases.Work.ListSessions;

namespace AgentLedger.UseCases.Work.GetSession;

// One session's summary (ADR 0023).
public sealed class GetSessionHandler(IWorkQueryService queries, TimeProvider clock)
  : IQueryHandler<GetSessionQuery, Result<SessionDetail>>
{
  public async ValueTask<Result<SessionDetail>> Handle(GetSessionQuery query, CancellationToken cancellationToken)
  {
    if (await queries.GetSessionAsync(query.SessionId, cancellationToken) is not { } detail)
    {
      return Result.NotFound();
    }

    return detail with { Session = ListSessionsHandler.MarkActive(detail.Session, clock.GetUtcNow()) };
  }
}
