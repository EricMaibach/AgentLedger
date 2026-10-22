using System.Security.Cryptography;
using AgentLedger.Core.UserAggregate;
using AgentLedger.Core.UserAggregate.Specifications;

namespace AgentLedger.UseCases.Users.ResetPassword;

// The way back in for a forgotten password (ADR 0018): run from inside the container, so only someone with
// Docker access on the host can use it. Returns the new password to print.
public sealed class ResetPasswordHandler(IRepository<User> repository, IPasswordHasher hasher)
  : ICommandHandler<ResetPasswordCommand, Result<string>>
{
  // No look-alike characters (0/o, 1/l/i), so it's easy to read off a terminal and type.
  private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
  private const int Length = 24;

  public async ValueTask<Result<string>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
  {
    if (!Username.TryFrom(command.Username, out var username)
        || await repository.FirstOrDefaultAsync(new UserByUsernameSpec(username), cancellationToken) is not { } user)
    {
      return Result.NotFound($"No user named '{command.Username}'.");
    }

    var password = RandomNumberGenerator.GetString(Alphabet, Length);
    user.ChangePassword(hasher.Hash(password), SecurityStamp.From(Guid.CreateVersion7()));
    await repository.UpdateAsync(user, cancellationToken);

    return password;
  }
}
