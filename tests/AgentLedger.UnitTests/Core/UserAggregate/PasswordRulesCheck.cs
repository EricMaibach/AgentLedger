using AgentLedger.Core.UserAggregate;
using Ardalis.Result;

namespace AgentLedger.UnitTests.Core.UserAggregate;

// NIST SP 800-63B (ADR 0018): length only, 15 to 128 characters, anything allowed.
public sealed class PasswordRulesCheck
{
  [Theory]
  [InlineData(15)]
  [InlineData(128)]
  public void AcceptsLengthsWithinTheLimits(int length)
  {
    PasswordRules.Check(new string('x', length)).IsSuccess.ShouldBeTrue();
  }

  [Theory]
  [InlineData(14)]
  [InlineData(129)]
  public void RejectsLengthsOutsideTheLimits(int length)
  {
    var result = PasswordRules.Check(new string('x', length));

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("password");
  }

  [Fact]
  public void RejectsMissingPassword()
  {
    PasswordRules.Check(null).Status.ShouldBe(ResultStatus.Invalid);
  }

  [Fact]
  public void AcceptsAPassphraseWithSpacesAndNoSymbols()
  {
    PasswordRules.Check("correct horse battery staple").IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public void CountsCharactersNotUtf16Units()
  {
    // Each emoji is one character but two UTF-16 units: 8 emoji are 8 characters, too short.
    PasswordRules.Check(string.Concat(Enumerable.Repeat("🔑", 8))).Status.ShouldBe(ResultStatus.Invalid);
    PasswordRules.Check(string.Concat(Enumerable.Repeat("🔑", 15))).IsSuccess.ShouldBeTrue();
  }
}
