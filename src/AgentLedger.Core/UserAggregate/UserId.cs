using Vogen;

namespace AgentLedger.Core.UserAggregate;

[ValueObject<Guid>]
public readonly partial struct UserId
{
  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("UserId cannot be empty.") : Validation.Ok;
}
