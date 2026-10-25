using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
  // Audit timestamps on every table (docs/architecture.md): shadow properties added by
  // AuditTimestampsConvention and set by AuditTimestampsInterceptor.
  public const string CreatedAt = nameof(CreatedAt);
  public const string UpdatedAt = nameof(UpdatedAt);

  public DbSet<AgentEventReceipt> AgentEventReceipts => Set<AgentEventReceipt>();
  public DbSet<User> Users => Set<User>();
  public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
  public DbSet<TranscriptLine> TranscriptLines => Set<TranscriptLine>();
  public DbSet<TranscriptFile> TranscriptFiles => Set<TranscriptFile>();

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
    configurationBuilder.Conventions.Add(_ => new AuditTimestampsConvention());

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  }

  public override int SaveChanges() =>
        SaveChangesAsync().GetAwaiter().GetResult();
}
