using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace AgentLedger.Infrastructure.Data;

// Adds the created_at / updated_at shadow properties to every entity type (docs/architecture.md).
// A finalizing convention runs after all configuration, so it only sees the real entity types,
// not types EF provisionally discovered and later mapped as converted or complex properties.
internal sealed class AuditTimestampsConvention : IModelFinalizingConvention
{
  public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
  {
    foreach (var entityType in modelBuilder.Metadata.GetEntityTypes().Where(t => !t.IsOwned()))
    {
      entityType.Builder.Property(typeof(DateTimeOffset), AppDbContext.CreatedAt)?.IsRequired(true);
      entityType.Builder.Property(typeof(DateTimeOffset), AppDbContext.UpdatedAt)?.IsRequired(true);
    }
  }
}
