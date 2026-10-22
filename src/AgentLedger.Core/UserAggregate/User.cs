namespace AgentLedger.Core.UserAggregate;

// Someone who can sign in to the web UI (ADR 0018). Holds the rules for wrong passwords and password changes;
// hashing and checking passwords happen outside the domain.
public sealed class User : EntityBase<User, UserId>, IAggregateRoot
{
  public const int MaxFailedSignIns = 5;
  public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

  public Username Username { get; private set; }
  public PasswordHash PasswordHash { get; private set; }
  public SecurityStamp SecurityStamp { get; private set; }
  public bool IsAdmin { get; private set; }
  public int FailedSignInCount { get; private set; }
  public DateTimeOffset? LockedUntil { get; private set; }

  public User(UserId id, Username username, PasswordHash passwordHash, SecurityStamp securityStamp, bool isAdmin)
  {
    Id = id;
    Username = username;
    PasswordHash = passwordHash;
    SecurityStamp = securityStamp;
    IsAdmin = isAdmin;
  }

  public bool IsLockedOut(DateTimeOffset now) => LockedUntil > now;

  public void RecordFailedSignIn(DateTimeOffset now)
  {
    // After a lockout ends, counting starts again from zero.
    if (LockedUntil is not null && !IsLockedOut(now))
    {
      FailedSignInCount = 0;
      LockedUntil = null;
    }

    FailedSignInCount++;
    if (FailedSignInCount >= MaxFailedSignIns)
    {
      LockedUntil = now + LockoutDuration;
    }
  }

  public void RecordSuccessfulSignIn()
  {
    FailedSignInCount = 0;
    LockedUntil = null;
  }

  // A new password ends other sessions (new stamp) and any lockout: a reset is how a locked-out user gets back in.
  public void ChangePassword(PasswordHash passwordHash, SecurityStamp securityStamp)
  {
    if (securityStamp == SecurityStamp)
    {
      throw new ArgumentException("A password change needs a new security stamp.", nameof(securityStamp));
    }

    PasswordHash = passwordHash;
    SecurityStamp = securityStamp;
    RecordSuccessfulSignIn();
  }

  // The same password, rehashed with stronger settings: sessions stay valid.
  public void UpgradePasswordHash(PasswordHash passwordHash) => PasswordHash = passwordHash;

  private User() { } // for EF Core
}
