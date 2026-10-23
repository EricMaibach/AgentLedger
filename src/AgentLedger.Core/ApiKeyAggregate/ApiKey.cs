using AgentLedger.Core.UserAggregate;

namespace AgentLedger.Core.ApiKeyAggregate;

// A personal API key (ADR 0019): everything sent with it is attributed to its owner. Never edited; revoked
// keys stay, for the record.
public sealed class ApiKey : EntityBase<ApiKey, ApiKeyId>, IAggregateRoot
{
  public const int MaxNameLength = 100;

  // "Last used" only needs to answer "is this key still in use?", so it's written at most this often.
  public static readonly TimeSpan LastUsedPrecision = TimeSpan.FromHours(1);

  public UserId OwnerId { get; private set; }
  public string Name { get; private set; }
  public ApiKeyScope Scope { get; private set; }
  public ApiKeySecretHash SecretHash { get; private set; }
  public DateTimeOffset IssuedAt { get; private set; }
  public DateTimeOffset? ExpiresAt { get; private set; }
  public DateTimeOffset? LastUsedAt { get; private set; }
  public DateTimeOffset? RevokedAt { get; private set; }
  public UserId? RevokedBy { get; private set; }

  public ApiKey(ApiKeyId id, UserId ownerId, string name, ApiKeyScope scope, ApiKeySecretHash secretHash,
                DateTimeOffset issuedAt, DateTimeOffset? expiresAt)
  {
    Id = id;
    OwnerId = ownerId;
    Name = Guard.Against.StringTooLong(Guard.Against.NullOrWhiteSpace(name).Trim(), MaxNameLength, nameof(name));
    Scope = Guard.Against.Null(scope);
    SecretHash = secretHash;
    IssuedAt = Guard.Against.Default(issuedAt);
    if (expiresAt <= issuedAt)
    {
      throw new ArgumentException("A key must expire after it was issued.", nameof(expiresAt));
    }

    ExpiresAt = expiresAt;
  }

  // The stored scope is what counts: a token edited to claim another scope doesn't match.
  public bool Matches(ApiKeyToken token) =>
    token.KeyId == Id && token.Scope == Scope && ApiKeySecretHash.Of(token.Secret).FixedTimeEquals(SecretHash);

  public bool IsActive(DateTimeOffset now) => RevokedAt is null && !(ExpiresAt <= now);

  // Permanent; revoking again keeps the first record of who did it and when.
  public void Revoke(UserId by, DateTimeOffset now)
  {
    if (RevokedAt is not null)
    {
      return;
    }

    RevokedBy = by;
    RevokedAt = Guard.Against.Default(now);
  }

  // Returns whether "last used" changed, so callers only save when it did.
  public bool RecordUse(DateTimeOffset now)
  {
    if (LastUsedAt is { } last && now - last < LastUsedPrecision)
    {
      return false;
    }

    LastUsedAt = now;
    return true;
  }

#pragma warning disable CS8618 // Required by EF Core: properties are set when a row is loaded
  private ApiKey() { }
#pragma warning restore CS8618
}
