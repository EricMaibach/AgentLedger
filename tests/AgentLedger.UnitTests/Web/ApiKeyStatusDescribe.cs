using AgentLedger.UseCases.ApiKeys;
using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

public sealed class ApiKeyStatusDescribe
{
  private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

  private static ApiKeyListItem Key(DateTimeOffset? expiresAt = null, DateTimeOffset? revokedAt = null, string? revokedBy = null) =>
    new(Guid.CreateVersion7(), "laptop", "Ingest", "eric", Now.AddDays(-100), expiresAt, null, revokedAt, revokedBy);

  [Fact]
  public void IsActiveWithoutExpiry()
  {
    ApiKeyStatus.Describe(Key(), Now).ShouldBe(new ApiKeyStatus("Active", IsActive: true));
  }

  [Fact]
  public void IsActiveWhenExpiryIsFarOff()
  {
    ApiKeyStatus.Describe(Key(expiresAt: Now.AddDays(15)), Now).Text.ShouldBe("Active");
  }

  [Theory]
  [InlineData(14 * 24, "Expires in 14 days")]
  [InlineData(30, "Expires in 2 days")]   // 30 hours: rounds up
  [InlineData(1, "Expires in 1 day")]
  public void WarnsWhenExpiryIsNear(int hours, string expected)
  {
    ApiKeyStatus.Describe(Key(expiresAt: Now.AddHours(hours)), Now).ShouldBe(new ApiKeyStatus(expected, IsActive: true));
  }

  [Fact]
  public void IsExpiredFromItsExpiry()
  {
    ApiKeyStatus.Describe(Key(expiresAt: Now), Now).ShouldBe(new ApiKeyStatus("Expired", IsActive: false));
  }

  [Fact]
  public void SaysWhoRevokedIt()
  {
    // Revoked wins over expired: it's the more deliberate fact.
    ApiKeyStatus.Describe(Key(expiresAt: Now.AddDays(-1), revokedAt: Now.AddDays(-2), revokedBy: "bob"), Now)
      .ShouldBe(new ApiKeyStatus("Revoked by bob", IsActive: false));
  }
}
