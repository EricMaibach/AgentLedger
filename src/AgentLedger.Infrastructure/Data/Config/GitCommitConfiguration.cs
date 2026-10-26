using System.Text.Json;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.GitAggregate;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AgentLedger.Infrastructure.Data.Config;

// Observed commits (ADR 0021). Written in bulk by GitObservationStore; this mapping is for reading, the schema
// and the migration.
public sealed class GitCommitConfiguration : IEntityTypeConfiguration<GitCommit>
{
  public void Configure(EntityTypeBuilder<GitCommit> builder)
  {
    builder.ToTable("git_commits");

    builder.HasKey(c => c.Id);
    builder.Property(c => c.Id).HasVogenConversion().ValueGeneratedNever(); // from repository and hash

    builder.Property(c => c.Repository).HasMaxLength(GitCommit.MaxRepositoryLength).IsRequired();
    builder.Property(c => c.Hash).HasVogenConversion().HasMaxLength(64).IsRequired();
    builder.Property(c => c.ParentHashes)
      .HasColumnType("text[]")
      .HasConversion(
        parents => parents.Select(p => p.Value).ToArray(),
        values => values.Select(CommitHash.From).ToList(),
        new ValueComparer<IReadOnlyList<CommitHash>>(
          (a, b) => a!.SequenceEqual(b!),
          parents => parents.Aggregate(0, (hash, parent) => HashCode.Combine(hash, parent)),
          parents => parents.ToList()))
      .IsRequired();
    builder.Property(c => c.AuthoredAt).IsRequired();
    builder.Property(c => c.CommittedAt).IsRequired();
    builder.Property(c => c.Message).HasColumnType("text").IsRequired();
    builder.Property(c => c.Files)
      .HasColumnType("jsonb")
      .HasConversion(
        files => GitCommitFilesJson.Write(files),
        json => GitCommitFilesJson.Read(json),
        new ValueComparer<IReadOnlyList<GitCommitFile>>(
          (a, b) => a!.SequenceEqual(b!),
          files => files.Aggregate(0, (hash, file) => HashCode.Combine(hash, file)),
          files => files.ToList()))
      .IsRequired();
    builder.Property(c => c.FilesTruncated).IsRequired();

    builder.Property(c => c.ApiKeyId).HasVogenConversion().IsRequired();
    builder.HasOne<ApiKey>().WithMany().HasForeignKey(c => c.ApiKeyId).OnDelete(DeleteBehavior.Restrict);
    builder.Property(c => c.ReceivedAt).IsRequired();

    builder.HasIndex(c => new { c.Repository, c.CommittedAt }); // a repository's commits in time order
  }
}

// The files column: [{"path": …, "change": "Renamed", "previousPath": …}], the change by name, as stored elsewhere.
internal static class GitCommitFilesJson
{
  private sealed record Row(string Path, string Change, string? PreviousPath);

  private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

  public static string Write(IReadOnlyList<GitCommitFile> files) =>
    JsonSerializer.Serialize(files.Select(f => new Row(f.Path, f.Change.Name, f.PreviousPath)), Options);

  public static List<GitCommitFile> Read(string json) =>
    [.. JsonSerializer.Deserialize<List<Row>>(json, Options)!.Select(r => new GitCommitFile(r.Path, GitFileChange.FromName(r.Change), r.PreviousPath))];
}
