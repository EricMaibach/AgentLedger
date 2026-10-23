using System.Security.Cryptography;
using System.Text;
using Vogen;

namespace AgentLedger.Core.ApiKeyAggregate;

// SHA-256 of a key's secret, in lowercase hex. A fast hash is right here: the secret is 32 random bytes, so
// unlike a password it can't be guessed, and ingest checks a key on every request (ADR 0019).
[ValueObject<string>]
public readonly partial struct ApiKeySecretHash
{
  public static ApiKeySecretHash Of(string secret) =>
    From(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret))));

  // Compares in constant time, so response timing reveals nothing about how much of a guess was right.
  public bool FixedTimeEquals(ApiKeySecretHash other) =>
    CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Value), Encoding.ASCII.GetBytes(other.Value));

  private static Validation Validate(string value) =>
    value.Length == 64 && value.All(char.IsAsciiHexDigitLower)
      ? Validation.Ok
      : Validation.Invalid("ApiKeySecretHash must be a SHA-256 in lowercase hex.");
}
