using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UnitTests.Core.ApiKeyAggregate;

// Test Data Builder: a valid key and the token that matches it. Tests change only the value they're about.
public sealed class ApiKeyBuilder
{
  public static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

  private ApiKeyId _id = ApiKeyId.From(Guid.CreateVersion7());
  private UserId _owner = UserId.From(Guid.CreateVersion7());
  private string _name = "dev container";
  private ApiKeyScope _scope = ApiKeyScope.Ingest;
  private string _secret = new('s', ApiKeyToken.SecretLength);
  private DateTimeOffset _createdAt = Now;
  private DateTimeOffset? _expiresAt;

  public ApiKeyBuilder WithId(ApiKeyId id) { _id = id; return this; }
  public ApiKeyBuilder WithOwner(UserId owner) { _owner = owner; return this; }
  public ApiKeyBuilder WithName(string name) { _name = name; return this; }
  public ApiKeyBuilder WithScope(ApiKeyScope scope) { _scope = scope; return this; }
  public ApiKeyBuilder WithSecret(string secret) { _secret = secret; return this; }
  public ApiKeyBuilder WithExpiresAt(DateTimeOffset? expiresAt) { _expiresAt = expiresAt; return this; }

  public ApiKeyToken Token => new(_scope, _id, _secret);

  public ApiKey Build() => new(_id, _owner, _name, _scope, ApiKeySecretHash.Of(_secret), _createdAt, _expiresAt);
}
