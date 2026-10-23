using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UnitTests.Core.ApiKeyAggregate;

public sealed class ApiKeyConstructor
{
  [Fact]
  public void StoresEveryValue()
  {
    var id = ApiKeyId.From(Guid.CreateVersion7());
    var owner = UserId.From(Guid.CreateVersion7());
    var hash = ApiKeySecretHash.Of("secret");

    var key = new ApiKey(id, owner, "laptop", ApiKeyScope.Read, hash, ApiKeyBuilder.Now, ApiKeyBuilder.Now.AddDays(30));

    key.Id.ShouldBe(id);
    key.OwnerId.ShouldBe(owner);
    key.Name.ShouldBe("laptop");
    key.Scope.ShouldBe(ApiKeyScope.Read);
    key.SecretHash.ShouldBe(hash);
    key.IssuedAt.ShouldBe(ApiKeyBuilder.Now);
    key.ExpiresAt.ShouldBe(ApiKeyBuilder.Now.AddDays(30));
    key.LastUsedAt.ShouldBeNull();
    key.RevokedAt.ShouldBeNull();
    key.RevokedBy.ShouldBeNull();
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void RequiresAName(string name)
  {
    Assert.ThrowsAny<ArgumentException>(() => new ApiKeyBuilder().WithName(name).Build());
  }

  [Fact]
  public void LimitsTheNameLength()
  {
    new ApiKeyBuilder().WithName(new string('n', ApiKey.MaxNameLength)).Build().Name.Length.ShouldBe(ApiKey.MaxNameLength);
    Assert.ThrowsAny<ArgumentException>(() => new ApiKeyBuilder().WithName(new string('n', ApiKey.MaxNameLength + 1)).Build());
  }

  [Fact]
  public void TrimsTheName()
  {
    new ApiKeyBuilder().WithName("  laptop ").Build().Name.ShouldBe("laptop");
  }

  [Fact]
  public void RejectsAnExpiryThatIsNotAfterIssue()
  {
    Assert.ThrowsAny<ArgumentException>(() => new ApiKeyBuilder().WithExpiresAt(ApiKeyBuilder.Now).Build());
  }
}
