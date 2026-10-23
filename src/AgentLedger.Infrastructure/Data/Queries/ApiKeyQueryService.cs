using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;
using AgentLedger.UseCases.ApiKeys;

namespace AgentLedger.Infrastructure.Data.Queries;

// API key read models, joined with the users who own and revoked them (ADR 0019). Never selects secret hashes.
public sealed class ApiKeyQueryService(AppDbContext db) : IApiKeyQueryService
{
  public async Task<IReadOnlyList<ApiKeyListItem>> ListAsync(UserId? owner, CancellationToken cancellationToken)
  {
    var keys = db.ApiKeys.AsNoTracking();
    if (owner is { } ownerId)
    {
      keys = keys.Where(k => k.OwnerId == ownerId);
    }

    var rows = await (
        from k in keys
        join o in db.Users on k.OwnerId equals o.Id
        join r in db.Users on k.RevokedBy equals (UserId?)r.Id into revokers
        from r in revokers.DefaultIfEmpty()
        orderby k.IssuedAt descending
        select new
        {
          k.Id,
          k.Name,
          k.Scope,
          Owner = o.Username,
          k.IssuedAt,
          k.ExpiresAt,
          k.LastUsedAt,
          k.RevokedAt,
          RevokedBy = r == null ? (Username?)null : r.Username,
        })
      .ToListAsync(cancellationToken);

    // In memory: unwrap value objects, which EF can't translate.
    return [.. rows.Select(r => new ApiKeyListItem(r.Id.Value, r.Name, r.Scope.Name, r.Owner.Value, r.IssuedAt, r.ExpiresAt,
      r.LastUsedAt, r.RevokedAt, r.RevokedBy?.Value))];
  }

  public async Task<IReadOnlyDictionary<ApiKeyId, ApiKeySender>> GetSendersAsync(IReadOnlyCollection<ApiKeyId> keyIds,
    CancellationToken cancellationToken)
  {
    var rows = await (
        from k in db.ApiKeys.AsNoTracking()
        where keyIds.Contains(k.Id)
        join o in db.Users on k.OwnerId equals o.Id
        select new { k.Id, k.Name, Owner = o.Username })
      .ToListAsync(cancellationToken);

    return rows.ToDictionary(r => r.Id, r => new ApiKeySender(r.Id.Value, r.Name, r.Owner.Value));
  }
}
