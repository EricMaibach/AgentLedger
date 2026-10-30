using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.UseCases.Enrollment;

namespace AgentLedger.Infrastructure.Data.Stores;

// Exchanging a code for a key (ADR 0024) in one transaction. The code is claimed with a conditional update (still
// unused), so of two machines racing with the same code only one gets a key. ExecuteUpdate bypasses the audit
// interceptor, so it sets updated_at itself.
public sealed class EnrollmentStore(AppDbContext db, TimeProvider clock) : IEnrollmentStore
{
  public async Task<bool> RedeemAsync(EnrollmentCode usedCode, ApiKey newKey, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    // The code changes only through the conditional update below. Loaded through this same context (as the handler
    // does), EF would otherwise save it with the key, already used, and the claim would never succeed.
    foreach (var entry in db.ChangeTracker.Entries<EnrollmentCode>().ToList())
      entry.State = EntityState.Detached;

    // The key first: the code row refers to it. If the claim below fails, the rollback removes the key too.
    db.ApiKeys.Add(newKey);
    await db.SaveChangesAsync(cancellationToken);

    var claimed = await db.Set<EnrollmentCode>()
      .Where(c => c.Id == usedCode.Id && c.UsedAt == null)
      .ExecuteUpdateAsync(set => set
        .SetProperty(c => c.UsedAt, usedCode.UsedAt)
        .SetProperty(c => c.UsedByHost, usedCode.UsedByHost)
        .SetProperty(c => c.ApiKeyId, usedCode.ApiKeyId)
        .SetProperty(c => EF.Property<DateTimeOffset>(c, AppDbContext.UpdatedAt), clock.GetUtcNow()), cancellationToken);
    if (claimed != 1)
    {
      await transaction.RollbackAsync(cancellationToken);
      db.ChangeTracker.Clear();
      return false;
    }

    await transaction.CommitAsync(cancellationToken);
    return true;
  }
}
