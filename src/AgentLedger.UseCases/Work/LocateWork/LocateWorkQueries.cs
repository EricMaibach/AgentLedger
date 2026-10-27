namespace AgentLedger.UseCases.Work.LocateWork;

// Where a turn or step lives, for the short links /work/turns/{id} and /work/steps/{id} (ADR 0023).
public sealed record LocateTurnQuery(Guid TurnId) : IQuery<Result<WorkLocation>>;

public sealed record LocateStepQuery(Guid StepId) : IQuery<Result<WorkLocation>>;
