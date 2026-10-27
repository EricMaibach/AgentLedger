namespace AgentLedger.UseCases.Work.GetStep;

public sealed record GetStepQuery(Guid StepId) : IQuery<Result<StepDetail>>;
