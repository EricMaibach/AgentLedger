using System.Text.Json;
using AgentLedger.Core.AgentEventAggregate;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AgentLedger.Infrastructure.Data.Config;

// Storage decisions: ADR 0009.
public sealed class AgentEventConfiguration : IEntityTypeConfiguration<AgentEvent>
{
  public void Configure(EntityTypeBuilder<AgentEvent> builder)
  {
    builder.ToTable("agent_events");

    builder.HasKey(e => e.Id);
    builder.Property(e => e.Id)
      .HasVogenConversion()
      .ValueGeneratedNever(); // client-generated UUIDv7; makes resends idempotent

    // Stored by name so rows are readable in SQL; names must never change once stored.
    builder.Property(e => e.Agent)
      .HasConversion(agent => agent.Name, name => AgentKind.FromName(name, false))
      .IsRequired();

    builder.Property(e => e.EventType).IsRequired();
    builder.Property(e => e.NativeSessionId);
    builder.Property(e => e.CapturedAt).IsRequired();
    builder.Property(e => e.ReceivedAt).IsRequired();

    builder.ComplexProperty(e => e.Context);

    builder.Property(e => e.Tags)
      .HasColumnType("jsonb")
      .HasConversion(
        tags => JsonSerializer.Serialize(tags, JsonSerializerOptions.Default),
        json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonSerializerOptions.Default)!,
        new ValueComparer<IReadOnlyDictionary<string, string>>(
          (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
          tags => tags.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag.Key, tag.Value)),
          tags => new Dictionary<string, string>(tags)))
      .IsRequired();

    // json, not jsonb: keeps the payload byte-for-byte.
    builder.Property(e => e.Payload)
      .HasColumnType("json")
      .IsRequired();

    builder.HasIndex(e => new { e.Agent, e.NativeSessionId });
    builder.HasIndex(e => e.CapturedAt);
    // Incremental-processing watermark. Declared here too so the index can reference it;
    // AppDbContext adds the audit timestamps to every entity after configurations run.
    builder.Property<DateTimeOffset>(AppDbContext.UpdatedAt);
    builder.HasIndex(AppDbContext.UpdatedAt);
    builder.HasIndex(e => e.Tags).HasMethod("gin");
  }
}
