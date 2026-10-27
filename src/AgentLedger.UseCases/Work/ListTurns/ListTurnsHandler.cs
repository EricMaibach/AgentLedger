namespace AgentLedger.UseCases.Work.ListTurns;

// A session's turns, oldest first (ADR 0023).
public sealed class ListTurnsHandler(IWorkQueryService queries) : IQueryHandler<ListTurnsQuery, Result<IReadOnlyList<TurnListItem>>>
{
  public async ValueTask<Result<IReadOnlyList<TurnListItem>>> Handle(ListTurnsQuery query, CancellationToken cancellationToken) =>
    await queries.ListTurnsAsync(query.SessionId, cancellationToken) is { } turns ? Result.Success(turns) : Result.NotFound();
}
