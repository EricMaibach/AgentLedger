using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.Core.EnrollmentAggregate;

// A one-time code that connects a machine (ADR 0024): created by a signed-in user, exchanged once, within ten
// minutes, for a new ingest key owned by that user. Kept after use, as the record of which machine was connected
// when and with which key.
public sealed class EnrollmentCode : EntityBase<EnrollmentCode, EnrollmentCodeId>, IAggregateRoot
{
  public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
  public const int MaxHostLength = 64;

  public UserId OwnerId { get; private set; }
  public EnrollmentCodeHash CodeHash { get; private set; }
  public DateTimeOffset CreatedAt { get; private set; }
  public DateTimeOffset ExpiresAt { get; private set; }
  public DateTimeOffset? UsedAt { get; private set; }
  public string? UsedByHost { get; private set; }
  public ApiKeyId? ApiKeyId { get; private set; }

  public EnrollmentCode(EnrollmentCodeId id, UserId ownerId, EnrollmentCodeHash codeHash, DateTimeOffset createdAt)
  {
    Id = id;
    OwnerId = ownerId;
    CodeHash = codeHash;
    CreatedAt = Guard.Against.Default(createdAt);
    ExpiresAt = CreatedAt + Lifetime;
  }

  public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

  public void Use(DateTimeOffset now, string host, ApiKeyId apiKeyId)
  {
    var trimmed = Guard.Against.StringTooLong(Guard.Against.NullOrWhiteSpace(host).Trim(), MaxHostLength, nameof(host));
    if (!IsUsable(now))
      throw new InvalidOperationException("This code has been used or has expired.");
    UsedAt = now;
    UsedByHost = trimmed;
    ApiKeyId = apiKeyId;
  }

#pragma warning disable CS8618 // Required by EF Core: properties are set when a row is loaded
  private EnrollmentCode() { }
#pragma warning restore CS8618
}
