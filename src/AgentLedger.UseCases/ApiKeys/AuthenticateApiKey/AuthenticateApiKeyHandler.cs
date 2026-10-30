using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.ApiKeys.AuthenticateApiKey;

// Checks a key presented with a request (ADR 0019). Every failure gives the same answer, so a caller learns
// nothing about which part was wrong. No caching: a revoked key fails on its very next request.
public sealed class AuthenticateApiKeyHandler(IRepository<ApiKey> keys, IReadRepository<User> users, TimeProvider clock)
  : ICommandHandler<AuthenticateApiKeyCommand, Result<ApiKeyHolder>>
{
  public async ValueTask<Result<ApiKeyHolder>> Handle(AuthenticateApiKeyCommand command, CancellationToken cancellationToken)
  {
    var now = clock.GetUtcNow();
    if (!ApiKeyToken.TryParse(command.Token, out var token)
        || await keys.GetByIdAsync(token.KeyId, cancellationToken) is not { } key
        || !key.Matches(token)
        || !key.IsActive(now)
        || await users.GetByIdAsync(key.OwnerId, cancellationToken) is not { } owner)
    {
      return Result.Unauthorized();
    }

    if (key.RecordUse(now, command.ClientVersion))
    {
      await keys.UpdateAsync(key, cancellationToken);
    }

    return new ApiKeyHolder(key.Id.Value, key.Name, key.Scope.Name, owner.Id.Value, owner.Username.Value);
  }
}
