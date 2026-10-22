using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.Users.CheckSession;

// A sign-in cookie stays valid only while the user exists and its security stamp is still the user's:
// a password change or reset changes the stamp, which ends every other session (ADR 0018).
public sealed class CheckSessionHandler(IReadRepository<User> repository) : IQueryHandler<CheckSessionQuery, Result>
{
  public async ValueTask<Result> Handle(CheckSessionQuery query, CancellationToken cancellationToken)
  {
    if (UserId.TryFrom(query.UserId, out var userId)
        && await repository.GetByIdAsync(userId, cancellationToken) is { } user
        && user.SecurityStamp.Value == query.SecurityStamp)
    {
      return Result.Success();
    }

    return Result.Unauthorized();
  }
}
