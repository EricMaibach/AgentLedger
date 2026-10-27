namespace AgentLedger.UseCases.Work.GetStep;

// Everything the details pane shows for one step, with its evidence (ADR 0023).
public sealed class GetStepHandler(IWorkQueryService queries) : IQueryHandler<GetStepQuery, Result<StepDetail>>
{
  public async ValueTask<Result<StepDetail>> Handle(GetStepQuery query, CancellationToken cancellationToken) =>
    await queries.GetStepAsync(query.StepId, cancellationToken) is { } step ? Result.Success(step) : Result.NotFound();
}
