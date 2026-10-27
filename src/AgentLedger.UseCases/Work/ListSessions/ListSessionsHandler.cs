namespace AgentLedger.UseCases.Work.ListSessions;

// Sessions, most recently active first (ADR 0023), each marked active when something happened in the last ten minutes.
public sealed class ListSessionsHandler(IWorkQueryService queries, TimeProvider clock)
  : IQueryHandler<ListSessionsQuery, Result<SessionPage>>
{
  public static readonly TimeSpan ActiveWithin = TimeSpan.FromMinutes(10);

  public async ValueTask<Result<SessionPage>> Handle(ListSessionsQuery query, CancellationToken cancellationToken)
  {
    if (query.Limit is < 1 or > Constants.MAX_PAGE_SIZE)
    {
      return Result.Invalid(new ValidationError("limit", $"Limit must be between 1 and {Constants.MAX_PAGE_SIZE}."));
    }

    var page = await queries.ListSessionsAsync(query.After, query.Limit, cancellationToken);
    var now = clock.GetUtcNow();
    return page with { Items = [.. page.Items.Select(s => MarkActive(s, now))] };
  }

  internal static SessionListItem MarkActive(SessionListItem session, DateTimeOffset now) =>
    session with { Active = session.LastActivityAt >= now - ActiveWithin };
}
