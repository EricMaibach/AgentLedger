using AgentLedger.Core.UserAggregate;
using AgentLedger.Core.UserAggregate.Specifications;

namespace AgentLedger.UseCases.Users.SignIn;

// ADR 0018: the same answer for an unknown username and a wrong password, with a real password check either
// way, so neither the message nor the timing reveals which usernames exist. Lockout after repeated failures.
public sealed class SignInHandler(IRepository<User> repository, IPasswordHasher hasher, TimeProvider clock)
  : ICommandHandler<SignInCommand, Result<SignedInUser>>
{
  public const string WrongCredentials = "Username or password is incorrect.";
  public const string TooManyAttempts = "Too many attempts. Try again in a few minutes.";

  public async ValueTask<Result<SignedInUser>> Handle(SignInCommand command, CancellationToken cancellationToken)
  {
    var user = Username.TryFrom(command.Username, out var username)
      ? await repository.FirstOrDefaultAsync(new UserByUsernameSpec(username), cancellationToken)
      : null;

    if (user is null)
    {
      hasher.Verify(hasher.UnusableHash, command.Password);
      return Result.Unauthorized(WrongCredentials);
    }

    var now = clock.GetUtcNow();
    if (user.IsLockedOut(now))
    {
      return Result.Forbidden(TooManyAttempts);
    }

    var check = hasher.Verify(user.PasswordHash, command.Password);
    if (!check.IsCorrect)
    {
      user.RecordFailedSignIn(now);
      await repository.UpdateAsync(user, cancellationToken);
      return user.IsLockedOut(now) ? Result.Forbidden(TooManyAttempts) : Result.Unauthorized(WrongCredentials);
    }

    user.RecordSuccessfulSignIn();
    if (check == PasswordCheck.SucceededRehashNeeded)
    {
      user.UpgradePasswordHash(hasher.Hash(command.Password));
    }

    await repository.UpdateAsync(user, cancellationToken);
    return user.ToSignedInUser();
  }
}
