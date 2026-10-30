using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.Core.UserAggregate;
using AgentLedger.UseCases.Enrollment;

namespace AgentLedger.Infrastructure.Data.Queries;

// Connected machines (ADR 0024): used enrollment codes joined with the keys they produced and their owners.
// Never selects secret hashes.
public sealed class EnrollmentQueryService(AppDbContext db) : IEnrollmentQueryService
{
  public async Task<IReadOnlyList<MachineListItem>> ListMachinesAsync(UserId? owner, CancellationToken cancellationToken)
  {
    var codes = db.Set<EnrollmentCode>().AsNoTracking().Where(c => c.UsedAt != null);
    if (owner is { } ownerId)
    {
      codes = codes.Where(c => c.OwnerId == ownerId);
    }

    var rows = await (
        from c in codes
        join k in db.ApiKeys on c.ApiKeyId equals (Core.ApiKeyAggregate.ApiKeyId?)k.Id
        join o in db.Users on c.OwnerId equals o.Id
        orderby c.UsedAt descending
        select new { k.Id, k.Name, c.UsedByHost, Owner = o.Username, c.UsedAt, k.LastUsedAt, k.LastClientVersion, k.RevokedAt, k.ExpiresAt })
      .ToListAsync(cancellationToken);

    // In memory: unwrap value objects, which EF can't translate.
    return [.. rows.Select(r => new MachineListItem(r.Id.Value, r.Name, r.UsedByHost!, r.Owner.Value, r.UsedAt!.Value, r.LastUsedAt,
      r.LastClientVersion, r.RevokedAt, r.ExpiresAt))];
  }
}
