using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.Infrastructure.Data.Config;

// Personal API keys (ADR 0019). Only the secret's hash is stored.
public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
  public void Configure(EntityTypeBuilder<ApiKey> builder)
  {
    builder.ToTable("api_keys");

    builder.HasKey(k => k.Id);
    builder.Property(k => k.Id)
      .HasVogenConversion()
      .ValueGeneratedNever();

    builder.Property(k => k.OwnerId).HasVogenConversion().IsRequired();
    builder.HasOne<User>().WithMany().HasForeignKey(k => k.OwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(k => k.OwnerId);

    builder.Property(k => k.Name).HasMaxLength(ApiKey.MaxNameLength).IsRequired();

    // Stored by name so rows are readable in SQL; names must never change once stored.
    builder.Property(k => k.Scope)
      .HasConversion(scope => scope.Name, name => ApiKeyScope.FromName(name, false))
      .IsRequired();

    builder.Property(k => k.SecretHash).HasVogenConversion().HasMaxLength(64).IsRequired();
    builder.Property(k => k.IssuedAt).IsRequired();
    builder.Property(k => k.ExpiresAt);
    builder.Property(k => k.LastUsedAt);
    builder.Property(k => k.RevokedAt);
    builder.Property(k => k.RevokedBy).HasConversion<VogenEfCoreConverters.UserIdEfCoreValueConverter>(); // nullable
    builder.HasOne<User>().WithMany().HasForeignKey(k => k.RevokedBy).OnDelete(DeleteBehavior.Restrict);
  }
}
