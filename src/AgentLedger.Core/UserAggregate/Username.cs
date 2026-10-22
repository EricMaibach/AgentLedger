using Vogen;

namespace AgentLedger.Core.UserAggregate;

// Case-insensitive: stored lowercase, so "Eric" and "eric" are the same user. Only ASCII letters, digits
// and . _ @ - so look-alike characters can't create a second "eric".
[ValueObject<string>]
public readonly partial struct Username
{
  public const int MaxLength = 64;

  private static string NormalizeInput(string input) => (input ?? string.Empty).Trim().ToLowerInvariant();

  private static Validation Validate(string value) =>
    value.Length is 0 or > MaxLength
      ? Validation.Invalid($"Username must be 1 to {MaxLength} characters.")
      : value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_' or '@' or '-')
        ? Validation.Ok
        : Validation.Invalid("Username may only contain letters, digits and . _ @ -");
}
