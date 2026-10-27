namespace AgentLedger.Infrastructure.Data.Projections;

// Projection tables (ADR 0022). A session's turns, and their steps and model responses, are deleted with it:
// ProjectionStore replaces a session by deleting its turns and inserting the new projection.
public sealed class SessionRowConfiguration : IEntityTypeConfiguration<SessionRow>
{
  public void Configure(EntityTypeBuilder<SessionRow> builder)
  {
    builder.ToTable("sessions");
    builder.HasKey(s => s.Id);
    builder.Property(s => s.Id).ValueGeneratedNever(); // content-derived (ADR 0022)
    builder.Property(s => s.Agent).IsRequired();
    builder.Property(s => s.NativeSessionId).IsRequired();
    builder.HasIndex(s => new { s.Agent, s.NativeSessionId }).IsUnique();
    builder.HasIndex(s => new { s.ProjectionVersion, s.LastActivityAt }); // sessions due for a rebuild
    builder.HasIndex(s => new { s.LastActivityAt, s.Id }); // the session list, most recent first (keyset)
  }
}

public sealed class TurnRowConfiguration : IEntityTypeConfiguration<TurnRow>
{
  public void Configure(EntityTypeBuilder<TurnRow> builder)
  {
    builder.ToTable("turns");
    builder.HasKey(t => t.Id);
    builder.Property(t => t.Id).ValueGeneratedNever();
    builder.Property(t => t.PromptId).IsRequired();
    builder.Property(t => t.Status).IsRequired();
    builder.Property(t => t.Source).IsRequired();
    builder.HasOne<SessionRow>().WithMany().HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Cascade);
    builder.HasIndex(t => new { t.SessionId, t.Sequence });
    builder.HasIndex(t => t.SourceEventIds).HasMethod("gin");
    builder.HasIndex(t => t.SourceLineIds).HasMethod("gin");
  }
}

public sealed class StepRowConfiguration : IEntityTypeConfiguration<StepRow>
{
  public void Configure(EntityTypeBuilder<StepRow> builder)
  {
    builder.ToTable("steps");
    builder.HasKey(s => s.Id);
    builder.Property(s => s.Id).ValueGeneratedNever();
    builder.Property(s => s.Kind).IsRequired();
    // text, not jsonb: a tool's input as the model sent it, which jsonb would reformat (and some can't be jsonb).
    builder.Property(s => s.ToolInput).HasColumnType("text");
    builder.HasOne<TurnRow>().WithMany().HasForeignKey(s => s.TurnId).OnDelete(DeleteBehavior.Cascade);
    builder.HasIndex(s => new { s.TurnId, s.Sequence });
    builder.HasIndex(s => s.SessionId);
    builder.HasIndex(s => s.SourceEventIds).HasMethod("gin");
    builder.HasIndex(s => s.SourceLineIds).HasMethod("gin");
  }
}

public sealed class ModelResponseRowConfiguration : IEntityTypeConfiguration<ModelResponseRow>
{
  public void Configure(EntityTypeBuilder<ModelResponseRow> builder)
  {
    builder.ToTable("model_responses");
    builder.HasKey(r => r.Id);
    builder.Property(r => r.Id).ValueGeneratedNever();
    builder.Property(r => r.MessageId).IsRequired();
    builder.Property(r => r.Model).IsRequired();
    builder.HasOne<TurnRow>().WithMany().HasForeignKey(r => r.TurnId).OnDelete(DeleteBehavior.Cascade);
    builder.HasIndex(r => r.TurnId);
    builder.HasIndex(r => r.SessionId);
    builder.HasIndex(r => r.SourceLineIds).HasMethod("gin");
  }
}

public sealed class ProjectionStateRowConfiguration : IEntityTypeConfiguration<ProjectionStateRow>
{
  public void Configure(EntityTypeBuilder<ProjectionStateRow> builder)
  {
    builder.ToTable("projection_state");
    builder.HasKey(s => s.Name);
  }
}
