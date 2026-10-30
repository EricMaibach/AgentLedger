namespace AgentLedger.UseCases.Enrollment.ListMachines;

public sealed record ListMachinesQuery(Guid ViewerId) : IQuery<Result<IReadOnlyList<MachineListItem>>>;
