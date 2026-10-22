using Vogen;

namespace AgentLedger.Core.UserAggregate;

// A hashed password, in the hasher's own format. A type of its own so a plain password can never be stored
// by mistake where a hash belongs.
[ValueObject<string>]
public readonly partial struct PasswordHash
{
  private static Validation Validate(string value) =>
    string.IsNullOrWhiteSpace(value) ? Validation.Invalid("PasswordHash cannot be blank.") : Validation.Ok;

  // Hashes must not end up in logs, where they could be attacked offline.
  public override string ToString() => "PasswordHash(redacted)";
}
