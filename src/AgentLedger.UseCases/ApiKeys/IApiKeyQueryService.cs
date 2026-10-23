using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.ApiKeys;

// Read models about API keys, joined with their owners (ADR 0016: query services for read models).
public interface IApiKeyQueryService
{
  // One owner's keys, or everyone's when owner is null; newest first.
  Task<IReadOnlyList<ApiKeyListItem>> ListAsync(UserId? owner, CancellationToken cancellationToken);

  // Who sent receipts with these keys: the key's name and its owner (ADR 0019).
  Task<IReadOnlyDictionary<ApiKeyId, ApiKeySender>> GetSendersAsync(IReadOnlyCollection<ApiKeyId> keyIds, CancellationToken cancellationToken);
}

public sealed record ApiKeyListItem(Guid KeyId, string Name, string Scope, string Owner, DateTimeOffset IssuedAt,
  DateTimeOffset? ExpiresAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt, string? RevokedBy);

public sealed record ApiKeySender(Guid KeyId, string KeyName, string Owner);
