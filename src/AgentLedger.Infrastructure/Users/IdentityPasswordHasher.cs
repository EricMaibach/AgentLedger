using System.Security.Cryptography;
using AgentLedger.Core.UserAggregate;
using AgentLedger.UseCases.Users;
using Microsoft.AspNetCore.Identity;

namespace AgentLedger.Infrastructure.Users;

// ASP.NET Core Identity's PasswordHasher (PBKDF2, salted, versioned format) behind our interface: the
// security-critical code is Microsoft's, not ours (ADR 0018).
public sealed class IdentityPasswordHasher : IPasswordHasher
{
  private static readonly PasswordHasher<User> Hasher = new();

  // Identity's hasher accepts a user for subclasses to use; the default implementation ignores it.
  private static readonly User NoUser = null!;

  // The hash of a random secret nobody knows, so no password matches it.
  private static readonly Lazy<PasswordHash> Unusable =
    new(() => PasswordHash.From(Hasher.HashPassword(NoUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)))));

  public PasswordHash UnusableHash => Unusable.Value;

  public PasswordHash Hash(string password) => PasswordHash.From(Hasher.HashPassword(NoUser, password));

  public PasswordCheck Verify(PasswordHash hash, string password) =>
    Hasher.VerifyHashedPassword(NoUser, hash.Value, password) switch
    {
      PasswordVerificationResult.Success => PasswordCheck.Succeeded,
      PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SucceededRehashNeeded,
      _ => PasswordCheck.Failed,
    };
}
