using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.Users;

// Hashes and checks passwords; implemented in Infrastructure with ASP.NET Core Identity's hasher (ADR 0018).
public interface IPasswordHasher
{
  PasswordHash Hash(string password);

  PasswordCheck Verify(PasswordHash hash, string password);

  // A hash no password matches. Checking against it when a username doesn't exist takes as long as a real
  // check, so response times don't reveal which usernames exist.
  PasswordHash UnusableHash { get; }
}
