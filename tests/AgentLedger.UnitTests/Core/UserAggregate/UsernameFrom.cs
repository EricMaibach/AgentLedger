using AgentLedger.Core.UserAggregate;
using Vogen;

namespace AgentLedger.UnitTests.Core.UserAggregate;

public sealed class UsernameFrom
{
  [Theory]
  [InlineData("eric")]
  [InlineData("eric.maibach")]
  [InlineData("eric_m-1")]
  [InlineData("eric@example.com")]
  public void AcceptsLettersDigitsAndCommonSeparators(string username)
  {
    Username.From(username).Value.ShouldBe(username);
  }

  [Fact]
  public void IsCaseInsensitiveAndIgnoresSurroundingSpaces()
  {
    // Stored lowercase: "Eric" and "eric" are the same user.
    Username.From("  Eric ").ShouldBe(Username.From("eric"));
    Username.From("  Eric ").Value.ShouldBe("eric");
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("eric maibach")]
  [InlineData("eric/admin")]
  [InlineData("éric")]
  public void RejectsBlankOrUnusualCharacters(string username)
  {
    Should.Throw<ValueObjectValidationException>(() => Username.From(username));
  }

  [Fact]
  public void AcceptsUpTo64Characters()
  {
    Username.From(new string('a', 64)).Value.Length.ShouldBe(64);
    Should.Throw<ValueObjectValidationException>(() => Username.From(new string('a', 65)));
  }
}
