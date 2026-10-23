using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.ApiKeys.RevokeApiKey;

// Owners revoke their own keys; admins anyone's. Other people's keys are "not found" for members, since
// they can't see them at all (ADR 0019).
public sealed class RevokeApiKeyHandler(IRepository<ApiKey> keys, IReadRepository<User> users, TimeProvider clock)
  : ICommandHandler<RevokeApiKeyCommand, Result>
{
  public async ValueTask<Result> Handle(RevokeApiKeyCommand command, CancellationToken cancellationToken)
  {
    if (!UserId.TryFrom(command.ActorId, out var actorId)
        || await users.GetByIdAsync(actorId, cancellationToken) is not { } actor
        || !ApiKeyId.TryFrom(command.KeyId, out var keyId)
        || await keys.GetByIdAsync(keyId, cancellationToken) is not { } key
        || (key.OwnerId != actor.Id && !actor.IsAdmin))
    {
      return Result.NotFound();
    }

    key.Revoke(actor.Id, clock.GetUtcNow());
    await keys.UpdateAsync(key, cancellationToken);
    return Result.Success();
  }
}
