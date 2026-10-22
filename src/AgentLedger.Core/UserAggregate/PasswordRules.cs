using System.Globalization;

namespace AgentLedger.Core.UserAggregate;

// NIST SP 800-63B for passwords as the only factor (ADR 0018): length is the only rule. No required
// symbols or digits, no forced changes; spaces and any other characters are allowed.
public static class PasswordRules
{
  public const int MinLength = 15;
  public const int MaxLength = 128;

  public static Result Check(string? password)
  {
    // Characters as people count them (an emoji is one), not UTF-16 units.
    var length = password is null ? 0 : new StringInfo(password).LengthInTextElements;

    return length is >= MinLength and <= MaxLength
      ? Result.Success()
      : Result.Invalid(new ValidationError("password", $"Password must be {MinLength} to {MaxLength} characters long. A few unrelated words make a good one."));
  }
}
