namespace AgentLedger.UseCases.Work.ListSteps;

// A turn's steps in order (ADR 0023), loaded when the turn is expanded.
public sealed class ListStepsHandler(IWorkQueryService queries) : IQueryHandler<ListStepsQuery, Result<IReadOnlyList<StepListItem>>>
{
  public async ValueTask<Result<IReadOnlyList<StepListItem>>> Handle(ListStepsQuery query, CancellationToken cancellationToken) =>
    await queries.ListStepsAsync(query.TurnId, cancellationToken) is { } steps ? Result.Success(steps) : Result.NotFound();
}
