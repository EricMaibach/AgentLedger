using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.Users.SetUpFirstAdmin;

// Creates the first admin from the setup page (ADR 0018): only while there are no users and the window is open.
public sealed class SetUpFirstAdminHandler(IRepository<User> repository, IPasswordHasher hasher, SetupWindow window)
  : ICommandHandler<SetUpFirstAdminCommand, Result<SignedInUser>>
{
  public async ValueTask<Result<SignedInUser>> Handle(SetUpFirstAdminCommand command, CancellationToken cancellationToken)
  {
    if (await repository.AnyAsync(cancellationToken))
    {
      return Result.Conflict("Setup is already complete.");
    }

    if (!window.IsOpen)
    {
      return Result.Unavailable("Setup timed out. Restart the ledger to try again.");
    }

    if (!Username.TryFrom(command.Username, out var username))
    {
      return Result.Invalid(new ValidationError("username", "Username may only contain letters, digits and . _ @ - (up to 64 characters)."));
    }

    var passwordCheck = PasswordRules.Check(command.Password);
    if (!passwordCheck.IsSuccess)
    {
      return Result.Invalid(passwordCheck.ValidationErrors);
    }

    // Two setups submitted at the same instant could both pass the check above; both people would have
    // to be on the setup page within the same moment, and the result (two admins) is visible and fixable.
    var user = new User(UserId.From(Guid.CreateVersion7()), username, hasher.Hash(command.Password),
      SecurityStamp.From(Guid.CreateVersion7()), isAdmin: true);
    await repository.AddAsync(user, cancellationToken);

    return Result.Created(user.ToSignedInUser());
  }
}
