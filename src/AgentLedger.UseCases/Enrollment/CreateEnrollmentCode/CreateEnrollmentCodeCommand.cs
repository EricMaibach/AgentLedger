namespace AgentLedger.UseCases.Enrollment.CreateEnrollmentCode;

public sealed record CreateEnrollmentCodeCommand(Guid OwnerId) : ICommand<Result<CreatedEnrollmentCode>>;

// The code is only ever available here, once (ADR 0024).
public sealed record CreatedEnrollmentCode(string Code, DateTimeOffset ExpiresAt);
