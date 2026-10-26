using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.GitAggregate;

namespace AgentLedger.Infrastructure.Data.Config;

// Observed tag changes (ADR 0021): appended, never updated.
public sealed class GitTagObservationConfiguration : IEntityTypeConfiguration<GitTagObservation>
{
  public void Configure(EntityTypeBuilder<GitTagObservation> builder)
  {
    builder.ToTable("git_tag_observations");

    builder.HasKey(t => t.Id);
    builder.Property(t => t.Id).HasVogenConversion().ValueGeneratedNever();

    builder.Property(t => t.Repository).HasMaxLength(GitCommit.MaxRepositoryLength).IsRequired();
    builder.Property(t => t.TagName).HasMaxLength(GitTagObservation.MaxTagNameLength).IsRequired();

    // Stored by name so rows are readable in SQL; names must never change once stored.
    builder.Property(t => t.Change).HasConversion(change => change.Name, name => GitTagChange.FromName(name, false)).IsRequired();

    // Nullable, so the generated converter is applied directly (HasVogenConversion only takes non-null types).
    builder.Property(t => t.Commit).HasConversion<VogenEfCoreConverters.CommitHashEfCoreValueConverter>().HasMaxLength(64);
    builder.Property(t => t.PreviousCommit).HasConversion<VogenEfCoreConverters.CommitHashEfCoreValueConverter>().HasMaxLength(64);

    builder.Property(t => t.ObservedAt).IsRequired();
    builder.Property(t => t.ApiKeyId).HasVogenConversion().IsRequired();
    builder.HasOne<ApiKey>().WithMany().HasForeignKey(t => t.ApiKeyId).OnDelete(DeleteBehavior.Restrict);
    builder.Property(t => t.ReceivedAt).IsRequired();

    builder.HasIndex(t => new { t.Repository, t.TagName, t.ObservedAt }); // a tag's history
  }
}
