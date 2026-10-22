using AgentLedger.Core.UserAggregate;
using Vogen;

namespace AgentLedger.UnitTests.Core.UserAggregate;

public sealed class UserIdFrom
{
  [Fact]
  public void AcceptsAGuid()
  {
    var guid = Guid.CreateVersion7();

    UserId.From(guid).Value.ShouldBe(guid);
  }

  [Fact]
  public void RejectsEmptyGuid()
  {
    Should.Throw<ValueObjectValidationException>(() => UserId.From(Guid.Empty));
  }
}

public sealed class SecurityStampFrom
{
  [Fact]
  public void RejectsEmptyGuid()
  {
    Should.Throw<ValueObjectValidationException>(() => SecurityStamp.From(Guid.Empty));
  }
}

public sealed class PasswordHashFrom
{
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void RejectsBlank(string hash)
  {
    Should.Throw<ValueObjectValidationException>(() => PasswordHash.From(hash));
  }

  [Fact]
  public void DoesNotRevealTheHashInToString()
  {
    // Hashes must not end up in logs, where they could be attacked offline.
    PasswordHash.From("AQAAAAIAAYagAAAAEhash").ToString().ShouldNotContain("AQAA");
  }
}
