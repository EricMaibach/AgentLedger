using System.Security.Cryptography;
using System.Text;
using Vogen;

namespace AgentLedger.Core.EnrollmentAggregate;

// A one-time code as shown to a person (ADR 0024): 8 characters, XXXX-XXXX, from an alphabet without look-alikes
// (no 0/O, 1/I/L), so it can be read aloud or retyped. About 40 bits: single use, ten minutes and a rate limit make
// guessing hopeless. Never stored: only its hash is.
[ValueObject<string>]
public readonly partial struct EnrollmentCodeText
{
  public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
  public const int Length = 8;

  // Random bytes in, so tests can pin the result; callers pass RandomNumberGenerator output. Bytes that would make
  // some characters likelier than others are skipped (rejection sampling).
  public static EnrollmentCodeText Generate(ReadOnlySpan<byte> random)
  {
    var limit = 256 - 256 % Alphabet.Length;
    var code = new StringBuilder(Length + 1);
    foreach (var b in random)
    {
      if (b >= limit)
        continue;
      if (code.Length == Length / 2)
        code.Append('-');
      code.Append(Alphabet[b % Alphabet.Length]);
      if (code.Length == Length + 1)
        return From(code.ToString());
    }
    throw new ArgumentException("Not enough random bytes for a code.", nameof(random));
  }

  public static EnrollmentCodeText New() => Generate(RandomNumberGenerator.GetBytes(64));

  private static Validation Validate(string value) =>
    value.Length == Length + 1 && value[Length / 2] == '-'
    && value.Remove(Length / 2, 1).All(c => Alphabet.Contains(c))
      ? Validation.Ok
      : Validation.Invalid("An enrollment code is XXXX-XXXX from the code alphabet.");
}

// SHA-256 of a code as meant, in lowercase hex: case, spaces and the dash don't matter, since people retype codes.
// A fast hash is fine: codes live ten minutes, work once and are rate-limited (ADR 0024).
[ValueObject<string>]
public readonly partial struct EnrollmentCodeHash
{
  public static EnrollmentCodeHash Of(string typed)
  {
    var meant = new string([.. Guard.Against.Null(typed).Where(c => c is not ('-' or ' ')).Select(char.ToUpperInvariant)]);
    return From(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(meant))));
  }

  private static Validation Validate(string value) =>
    value.Length == 64 && value.All(char.IsAsciiHexDigitLower)
      ? Validation.Ok
      : Validation.Invalid("EnrollmentCodeHash must be a SHA-256 in lowercase hex.");
}

[ValueObject<Guid>]
public readonly partial struct EnrollmentCodeId
{
  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("EnrollmentCodeId cannot be empty.") : Validation.Ok;
}
