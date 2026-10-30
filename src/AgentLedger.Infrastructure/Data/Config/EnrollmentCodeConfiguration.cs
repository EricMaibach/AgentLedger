using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.Infrastructure.Data.Config;

// One-time codes for connecting machines (ADR 0024). Only the code's hash is stored.
public sealed class EnrollmentCodeConfiguration : IEntityTypeConfiguration<EnrollmentCode>
{
  public void Configure(EntityTypeBuilder<EnrollmentCode> builder)
  {
    builder.ToTable("enrollment_codes");

    builder.HasKey(c => c.Id);
    builder.Property(c => c.Id).HasVogenConversion().ValueGeneratedNever();
    builder.Property(c => c.OwnerId).HasVogenConversion().IsRequired();
    builder.HasOne<User>().WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.Restrict);
    builder.Property(c => c.CodeHash).HasVogenConversion().HasMaxLength(64).IsRequired();
    builder.HasIndex(c => c.CodeHash).IsUnique();
    builder.Property(c => c.CreatedAt).IsRequired();
    builder.Property(c => c.ExpiresAt).IsRequired();
    builder.Property(c => c.UsedByHost).HasMaxLength(EnrollmentCode.MaxHostLength);
    builder.Property(c => c.ApiKeyId).HasConversion<VogenEfCoreConverters.ApiKeyIdEfCoreValueConverter>(); // nullable
    builder.HasOne<ApiKey>().WithMany().HasForeignKey(c => c.ApiKeyId).OnDelete(DeleteBehavior.Restrict);
  }
}
