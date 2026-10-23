using Vogen;

namespace AgentLedger.Core.ApiKeyAggregate;

// The key's identity. Also its public ID inside the token (in base62), so a presented key is found with
// one lookup, and receipts reference it (ADR 0019).
[ValueObject<Guid>]
public readonly partial struct ApiKeyId
{
  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("ApiKeyId cannot be empty.") : Validation.Ok;
}
