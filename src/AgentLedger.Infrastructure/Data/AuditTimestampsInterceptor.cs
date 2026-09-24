using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentLedger.Infrastructure.Data;

// Sets the created_at / updated_at shadow properties that every table carries.
public sealed class AuditTimestampsInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
    InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    if (eventData.Context is not null)
    {
      StampTimestamps(eventData.Context);
    }

    return base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private void StampTimestamps(DbContext context)
  {
    var now = timeProvider.GetUtcNow();

    foreach (var entry in context.ChangeTracker.Entries())
    {
      if (entry.State == EntityState.Added)
      {
        entry.Property(AppDbContext.CreatedAt).CurrentValue = now;
        entry.Property(AppDbContext.UpdatedAt).CurrentValue = now;
      }
      else if (entry.State == EntityState.Modified)
      {
        entry.Property(AppDbContext.UpdatedAt).CurrentValue = now;
      }
    }
  }
}
