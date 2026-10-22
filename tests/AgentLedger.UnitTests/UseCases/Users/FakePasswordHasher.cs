using AgentLedger.Core.UserAggregate;
using AgentLedger.UseCases.Users;

namespace AgentLedger.UnitTests.UseCases.Users;

// Readable stand-in for the real (slow, salted) hasher: "hashed:<password>". Hashes starting with "old:"
// verify but ask for a rehash, like Identity's older formats. Records every check, so tests can see that a
// password check ran even for an unknown username.
public sealed class FakePasswordHasher : IPasswordHasher
{
  public List<PasswordHash> Checked { get; } = [];

  public PasswordHash UnusableHash { get; } = PasswordHash.From("unusable");

  public PasswordHash Hash(string password) => PasswordHash.From($"hashed:{password}");

  public PasswordCheck Verify(PasswordHash hash, string password)
  {
    Checked.Add(hash);
    return hash.Value == $"hashed:{password}" ? PasswordCheck.Succeeded
      : hash.Value == $"old:{password}" ? PasswordCheck.SucceededRehashNeeded
      : PasswordCheck.Failed;
  }
}
