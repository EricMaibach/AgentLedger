using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UnitTests.Core.UserAggregate;

// Test Data Builder: every test starts from a valid user and changes only the value it cares about.
public sealed class UserBuilder
{
  public static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

  private UserId _id = UserId.From(Guid.CreateVersion7());
  private Username _username = Username.From("eric");
  private PasswordHash _passwordHash = PasswordHash.From("AQAAAAIAAYagAAAAEhash");
  private SecurityStamp _securityStamp = SecurityStamp.From(Guid.CreateVersion7());
  private bool _isAdmin = true;

  public UserBuilder WithId(UserId id) { _id = id; return this; }
  public UserBuilder WithUsername(string username) { _username = Username.From(username); return this; }
  public UserBuilder WithPasswordHash(PasswordHash passwordHash) { _passwordHash = passwordHash; return this; }
  public UserBuilder WithSecurityStamp(SecurityStamp securityStamp) { _securityStamp = securityStamp; return this; }
  public UserBuilder WithIsAdmin(bool isAdmin) { _isAdmin = isAdmin; return this; }

  public User Build() => new(_id, _username, _passwordHash, _securityStamp, _isAdmin);
}
