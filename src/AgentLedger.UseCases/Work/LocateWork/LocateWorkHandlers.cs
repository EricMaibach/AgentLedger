namespace AgentLedger.UseCases.Work.LocateWork;

public sealed class LocateTurnHandler(IWorkQueryService queries) : IQueryHandler<LocateTurnQuery, Result<WorkLocation>>
{
  public async ValueTask<Result<WorkLocation>> Handle(LocateTurnQuery query, CancellationToken cancellationToken) =>
    await queries.LocateTurnAsync(query.TurnId, cancellationToken) is { } location ? Result.Success(location) : Result.NotFound();
}

public sealed class LocateStepHandler(IWorkQueryService queries) : IQueryHandler<LocateStepQuery, Result<WorkLocation>>
{
  public async ValueTask<Result<WorkLocation>> Handle(LocateStepQuery query, CancellationToken cancellationToken) =>
    await queries.LocateStepAsync(query.StepId, cancellationToken) is { } location ? Result.Success(location) : Result.NotFound();
}
