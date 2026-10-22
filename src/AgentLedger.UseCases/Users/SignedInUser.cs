using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.Users;

// What a sign-in cookie carries (ADR 0018).
public sealed record SignedInUser(Guid UserId, string Username, Guid SecurityStamp, bool IsAdmin);

internal static class SignedInUserMapping
{
  public static SignedInUser ToSignedInUser(this User user) =>
    new(user.Id.Value, user.Username.Value, user.SecurityStamp.Value, user.IsAdmin);
}
