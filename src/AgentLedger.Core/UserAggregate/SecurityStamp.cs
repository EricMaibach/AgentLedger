using Vogen;

namespace AgentLedger.Core.UserAggregate;

// Changes whenever a user's password does; sign-in cookies carry it, and a cookie whose stamp no longer
// matches is rejected, which ends the user's other sessions (ADR 0018).
[ValueObject<Guid>]
public readonly partial struct SecurityStamp
{
  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("SecurityStamp cannot be empty.") : Validation.Ok;
}
