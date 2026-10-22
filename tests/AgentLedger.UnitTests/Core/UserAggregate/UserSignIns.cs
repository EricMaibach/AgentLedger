using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UnitTests.Core.UserAggregate;

// Lockout (ADR 0018): 5 wrong passwords in a row lock the user for 15 minutes.
public sealed class UserRecordFailedSignIn
{
  private static readonly DateTimeOffset Now = UserBuilder.Now;

  [Fact]
  public void DoesNotLockBeforeTheFifthFailure()
  {
    var user = new UserBuilder().Build();

    FailTimes(user, 4);

    user.FailedSignInCount.ShouldBe(4);
    user.IsLockedOut(Now).ShouldBeFalse();
  }

  [Fact]
  public void LocksForFifteenMinutesAtTheFifthFailure()
  {
    var user = new UserBuilder().Build();

    FailTimes(user, 5);

    user.LockedUntil.ShouldBe(Now.AddMinutes(15));
    user.IsLockedOut(Now.AddMinutes(15).AddTicks(-1)).ShouldBeTrue();
    user.IsLockedOut(Now.AddMinutes(15)).ShouldBeFalse();
  }

  [Fact]
  public void StartsCountingAgainAfterALockout()
  {
    // Once the lockout ends, one more wrong password must not lock again immediately.
    var user = new UserBuilder().Build();
    FailTimes(user, 5);

    user.RecordFailedSignIn(Now.AddMinutes(16));

    user.FailedSignInCount.ShouldBe(1);
    user.IsLockedOut(Now.AddMinutes(16)).ShouldBeFalse();
  }

  private static void FailTimes(User user, int times)
  {
    for (var i = 0; i < times; i++)
    {
      user.RecordFailedSignIn(Now);
    }
  }
}

public sealed class UserRecordSuccessfulSignIn
{
  [Fact]
  public void ResetsFailedSignIns()
  {
    var user = new UserBuilder().Build();
    user.RecordFailedSignIn(UserBuilder.Now);
    user.RecordFailedSignIn(UserBuilder.Now);

    user.RecordSuccessfulSignIn();

    user.FailedSignInCount.ShouldBe(0);
    user.LockedUntil.ShouldBeNull();
  }
}

public sealed class UserChangePassword
{
  [Fact]
  public void ReplacesTheHashAndTheSecurityStamp()
  {
    // A new stamp ends the user's other sessions (ADR 0018).
    var user = new UserBuilder().Build();
    var hash = PasswordHash.From("AQAAAAIAAYagAAAAEnew");
    var stamp = SecurityStamp.From(Guid.CreateVersion7());

    user.ChangePassword(hash, stamp);

    user.PasswordHash.ShouldBe(hash);
    user.SecurityStamp.ShouldBe(stamp);
  }

  [Fact]
  public void EndsALockout()
  {
    // A reset is how a locked-out user gets back in before the lockout ends.
    var user = new UserBuilder().Build();
    for (var i = 0; i < 5; i++)
    {
      user.RecordFailedSignIn(UserBuilder.Now);
    }

    user.ChangePassword(PasswordHash.From("AQAAAAIAAYagAAAAEnew"), SecurityStamp.From(Guid.CreateVersion7()));

    user.IsLockedOut(UserBuilder.Now).ShouldBeFalse();
    user.FailedSignInCount.ShouldBe(0);
  }

  [Fact]
  public void RejectsReusingTheCurrentSecurityStamp()
  {
    var stamp = SecurityStamp.From(Guid.CreateVersion7());
    var user = new UserBuilder().WithSecurityStamp(stamp).Build();

    Assert.ThrowsAny<ArgumentException>(() => user.ChangePassword(PasswordHash.From("AQAAAAIAAYagAAAAEnew"), stamp));
  }
}

public sealed class UserUpgradePasswordHash
{
  [Fact]
  public void ReplacesTheHashButKeepsSessions()
  {
    // Same password, stronger hash (the hasher asked for a rehash): not a password change.
    var stamp = SecurityStamp.From(Guid.CreateVersion7());
    var user = new UserBuilder().WithSecurityStamp(stamp).Build();
    var upgraded = PasswordHash.From("AQAAAAIAAYagAAAAEupgraded");

    user.UpgradePasswordHash(upgraded);

    user.PasswordHash.ShouldBe(upgraded);
    user.SecurityStamp.ShouldBe(stamp);
  }
}
