namespace AgentLedger.UseCases.Work.ListSteps;

public sealed record ListStepsQuery(Guid TurnId) : IQuery<Result<IReadOnlyList<StepListItem>>>;
