using System.Security.Cryptography;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.ApiKeys.CreateApiKey;

// Creates a personal key (ADR 0019). The token is returned once; only its secret's hash is stored.
public sealed class CreateApiKeyHandler(IRepository<ApiKey> keys, IReadRepository<User> users, TimeProvider clock)
  : ICommandHandler<CreateApiKeyCommand, Result<CreatedApiKey>>
{
  public static readonly IReadOnlyList<int> ExpiryChoicesInDays = [30, 90, 365];

  public async ValueTask<Result<CreatedApiKey>> Handle(CreateApiKeyCommand command, CancellationToken cancellationToken)
  {
    if (!ApiKeyScope.TryFromName(command.Scope, ignoreCase: true, out var scope))
    {
      return Result.Invalid(new ValidationError("scope", "Scope must be 'ingest' or 'read'."));
    }

    if (command.ExpiresInDays is { } days && !ExpiryChoicesInDays.Contains(days))
    {
      return Result.Invalid(new ValidationError("expiresInDays", "Expiry must be none, 30, 90 or 365 days."));
    }

    if (!UserId.TryFrom(command.OwnerId, out var ownerId) || await users.GetByIdAsync(ownerId, cancellationToken) is null)
    {
      return Result.NotFound();
    }

    var now = clock.GetUtcNow();
    var id = ApiKeyId.From(Guid.CreateVersion7());
    var token = ApiKeyToken.Create(scope, id, RandomNumberGenerator.GetBytes(ApiKeyToken.SecretBytes));

    ApiKey key;
    try
    {
      key = new ApiKey(id, ownerId, command.Name, scope, ApiKeySecretHash.Of(token.Secret), now,
        command.ExpiresInDays is { } d ? now.AddDays(d) : null);
    }
    catch (ArgumentException ex)
    {
      return Result.Invalid(new ValidationError(ex.ParamName, ex.Message));
    }

    await keys.AddAsync(key, cancellationToken);
    return Result.Created(new CreatedApiKey(id.Value, token.Value));
  }
}
