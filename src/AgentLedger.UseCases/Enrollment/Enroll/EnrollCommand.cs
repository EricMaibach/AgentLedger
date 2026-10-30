namespace AgentLedger.UseCases.Enrollment.Enroll;

// From a machine being connected (ADR 0024): the code it was given and its host name.
public sealed record EnrollCommand(string Code, string Host) : ICommand<Result<EnrolledMachine>>;

// The machine's new ingest key: the token only ever available here, once.
public sealed record EnrolledMachine(Guid KeyId, string KeyName, string Token);
