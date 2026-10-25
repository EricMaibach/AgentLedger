using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.Infrastructure.Data.Config;

// Transcript lines and files (ADR 0020). Written by TranscriptStore in bulk; this mapping is for reading,
// for the schema and for the migration.
public sealed class TranscriptLineConfiguration : IEntityTypeConfiguration<TranscriptLine>
{
  public void Configure(EntityTypeBuilder<TranscriptLine> builder)
  {
    builder.ToTable("transcript_lines");

    builder.HasKey(l => l.Id);
    builder.Property(l => l.Id).HasVogenConversion().ValueGeneratedNever(); // computed from content

    // Stored by name so rows are readable in SQL; names must never change once stored.
    builder.Property(l => l.Agent).HasConversion(agent => agent.Name, name => AgentKind.FromName(name, false)).IsRequired();
    builder.Property(l => l.NativeSessionId).IsRequired();
    builder.Property(l => l.File).HasVogenConversion().IsRequired();
    builder.Property(l => l.SubagentId).HasMaxLength(TranscriptFileName.MaxSubagentIdLength);
    builder.Property(l => l.ByteOffset).IsRequired();

    // text, not json: exactly the file's bytes, valid JSON or not (ADR 0020). Postgres compresses long values.
    builder.Property(l => l.Text).HasColumnType("text").IsRequired();

    // The line's own top-level "type" field, taken when it was stored (ADR 0020): filtered like an event's type.
    builder.Property(l => l.LineType);

    builder.Property(l => l.ApiKeyId).HasVogenConversion().IsRequired();
    builder.HasOne<ApiKey>().WithMany().HasForeignKey(l => l.ApiKeyId).OnDelete(DeleteBehavior.Restrict);
    builder.Property(l => l.ReceivedAt).IsRequired();

    // Reading a file's lines in order.
    builder.HasIndex(l => new { l.Agent, l.NativeSessionId, l.File, l.ByteOffset });
    // The raw transcripts list, newest first: ORDER BY received_at DESC, byte_offset DESC, id DESC (ADR 0020).
    builder.HasIndex(l => new { l.ReceivedAt, l.ByteOffset, l.Id });
    builder.HasIndex(l => l.ApiKeyId);
  }
}

public sealed class TranscriptFileConfiguration : IEntityTypeConfiguration<TranscriptFile>
{
  public void Configure(EntityTypeBuilder<TranscriptFile> builder)
  {
    builder.ToTable("transcript_files");

    builder.HasKey(f => f.Id);
    builder.Property(f => f.Id).HasVogenConversion().ValueGeneratedNever(); // computed from agent, session, file

    builder.Property(f => f.Agent).HasConversion(agent => agent.Name, name => AgentKind.FromName(name, false)).IsRequired();
    builder.Property(f => f.NativeSessionId).IsRequired();
    builder.Property(f => f.File).HasVogenConversion().IsRequired();
    builder.Property(f => f.SubagentId).HasMaxLength(TranscriptFileName.MaxSubagentIdLength);
    builder.Property(f => f.Meta).HasColumnType("text"); // the side file exactly as read

    builder.Property(f => f.ApiKeyId).HasVogenConversion().IsRequired();
    builder.HasOne<ApiKey>().WithMany().HasForeignKey(f => f.ApiKeyId).OnDelete(DeleteBehavior.Restrict);
    builder.Property(f => f.FirstSeenAt).IsRequired();

    builder.HasIndex(f => new { f.Agent, f.NativeSessionId }); // a session's files
  }
}
