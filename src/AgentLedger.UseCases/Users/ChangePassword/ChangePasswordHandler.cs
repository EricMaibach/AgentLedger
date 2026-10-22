using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.Users.ChangePassword;

// A signed-in user changes their own password. A wrong current password counts as a failed sign-in, so a
// stolen session can't be used to guess the password. The new security stamp ends the user's other sessions.
public sealed class ChangePasswordHandler(IRepository<User> repository, IPasswordHasher hasher, TimeProvider clock)
  : ICommandHandler<ChangePasswordCommand, Result<SignedInUser>>
{
  public async ValueTask<Result<SignedInUser>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
  {
    if (!UserId.TryFrom(command.UserId, out var userId)
        || await repository.GetByIdAsync(userId, cancellationToken) is not { } user)
    {
      return Result.NotFound();
    }

    var now = clock.GetUtcNow();
    if (user.IsLockedOut(now))
    {
      return Result.Forbidden(SignIn.SignInHandler.TooManyAttempts);
    }

    var rules = PasswordRules.Check(command.NewPassword);
    if (!rules.IsSuccess)
    {
      return Result.Invalid(rules.ValidationErrors.Select(e => new ValidationError("newPassword", e.ErrorMessage)));
    }

    if (!hasher.Verify(user.PasswordHash, command.CurrentPassword).IsCorrect)
    {
      user.RecordFailedSignIn(now);
      await repository.UpdateAsync(user, cancellationToken);
      return Result.Invalid(new ValidationError("currentPassword", "Current password is incorrect."));
    }

    user.ChangePassword(hasher.Hash(command.NewPassword), SecurityStamp.From(Guid.CreateVersion7()));
    await repository.UpdateAsync(user, cancellationToken);

    return user.ToSignedInUser();
  }
}
