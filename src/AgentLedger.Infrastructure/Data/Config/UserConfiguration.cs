using AgentLedger.Core.UserAggregate;

namespace AgentLedger.Infrastructure.Data.Config;

// Sign-in users (ADR 0018).
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
  public void Configure(EntityTypeBuilder<User> builder)
  {
    builder.ToTable("users");

    builder.HasKey(u => u.Id);
    builder.Property(u => u.Id)
      .HasVogenConversion()
      .ValueGeneratedNever();

    // Stored lowercase by Username itself, so a plain unique index makes usernames case-insensitively unique.
    builder.Property(u => u.Username)
      .HasVogenConversion()
      .HasMaxLength(Username.MaxLength)
      .IsRequired();
    builder.HasIndex(u => u.Username).IsUnique();

    builder.Property(u => u.PasswordHash).HasVogenConversion().IsRequired();
    builder.Property(u => u.SecurityStamp).HasVogenConversion().IsRequired();
    builder.Property(u => u.IsAdmin).IsRequired();
    builder.Property(u => u.FailedSignInCount).IsRequired();
    builder.Property(u => u.LockedUntil);
  }
}
