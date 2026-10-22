using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UnitTests.Core.UserAggregate;

public sealed class UserConstructor
{
  [Fact]
  public void StoresEveryValue()
  {
    var id = UserId.From(Guid.CreateVersion7());
    var hash = PasswordHash.From("AQAAAAIAAYagAAAAEhash");
    var stamp = SecurityStamp.From(Guid.CreateVersion7());

    var user = new User(id, Username.From("eric"), hash, stamp, isAdmin: true);

    user.Id.ShouldBe(id);
    user.Username.ShouldBe(Username.From("eric"));
    user.PasswordHash.ShouldBe(hash);
    user.SecurityStamp.ShouldBe(stamp);
    user.IsAdmin.ShouldBeTrue();
  }

  [Fact]
  public void StartsWithNoFailedSignInsAndUnlocked()
  {
    var user = new UserBuilder().Build();

    user.FailedSignInCount.ShouldBe(0);
    user.LockedUntil.ShouldBeNull();
    user.IsLockedOut(UserBuilder.Now).ShouldBeFalse();
  }
}
