using AgentLedger.Core.UserAggregate;
using AgentLedger.Infrastructure.Users;
using AgentLedger.UseCases.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AgentLedger.IntegrationTests.Users;

public sealed class IdentityPasswordHasherTests
{
  private const string Password = "correct horse battery staple";

  private readonly IdentityPasswordHasher _hasher = new();

  [Fact]
  public void AcceptsTheRightPassword()
  {
    var hash = _hasher.Hash(Password);

    _hasher.Verify(hash, Password).ShouldBe(PasswordCheck.Succeeded);
  }

  [Fact]
  public void RejectsAWrongPassword()
  {
    var hash = _hasher.Hash(Password);

    _hasher.Verify(hash, "correct horse battery stapler").ShouldBe(PasswordCheck.Failed);
  }

  [Fact]
  public void NeverStoresThePasswordItself()
  {
    var hash = _hasher.Hash(Password);

    hash.Value.ShouldNotContain(Password);
    _hasher.Hash(Password).ShouldNotBe(hash); // salted: the same password hashes differently each time
  }

  [Fact]
  public void AsksForARehashOfAnOlderFormat()
  {
    // Identity's V2 format (PBKDF2 with SHA-1, 1000 iterations): still accepted, but should be upgraded.
    var v2 = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
    {
      CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2,
    }));
    var oldHash = PasswordHash.From(v2.HashPassword(null!, Password)); // the user argument is unused by this hasher

    _hasher.Verify(oldHash, Password).ShouldBe(PasswordCheck.SucceededRehashNeeded);
  }

  [Fact]
  public void HasAnUnusableHashThatNoPasswordMatches()
  {
    _hasher.Verify(_hasher.UnusableHash, Password).ShouldBe(PasswordCheck.Failed);
    _hasher.Verify(_hasher.UnusableHash, "").ShouldBe(PasswordCheck.Failed);
  }
}
