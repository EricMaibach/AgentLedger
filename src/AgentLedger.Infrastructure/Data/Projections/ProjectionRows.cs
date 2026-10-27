namespace AgentLedger.Infrastructure.Data.Projections;

// Projection tables (ADR 0022): plain rows, rebuilt a session at a time from the raw tables by ProjectionStore.
// The model they store lives in Core (AgentLedger.Core.Projections); these are its storage shape, read by query
// services. Enumerations are stored by name, lineage as arrays of raw IDs (GIN-indexed for "which rows use this?").

public sealed class SessionRow
{
  public Guid Id { get; set; }
  public string Agent { get; set; } = "";
  public string NativeSessionId { get; set; } = "";
  public DateTimeOffset StartedAt { get; set; }
  public DateTimeOffset LastActivityAt { get; set; }
  public int TurnCount { get; set; }

  // The projection version that built this session: lower than the current one means it's due for a rebuild.
  public int ProjectionVersion { get; set; }
}

public sealed class TurnRow
{
  public Guid Id { get; set; }
  public Guid SessionId { get; set; }
  public int Sequence { get; set; }
  public string PromptId { get; set; } = "";
  public string Status { get; set; } = "";
  public string Source { get; set; } = "";
  public DateTimeOffset StartedAt { get; set; }
  public DateTimeOffset? EndedAt { get; set; }
  public string? GitRepo { get; set; }
  public string? GitBranch { get; set; }
  public string? GitCommit { get; set; }
  public string? GitWorktree { get; set; }
  public string? Directory { get; set; }
  public string? Host { get; set; }
  public string? User { get; set; }
  public Guid[] SourceEventIds { get; set; } = [];
  public Guid[] SourceLineIds { get; set; } = [];
}

public sealed class StepRow
{
  public Guid Id { get; set; }
  public Guid TurnId { get; set; }
  public Guid SessionId { get; set; }
  public int Sequence { get; set; }
  public string Kind { get; set; } = "";
  public DateTimeOffset StartedAt { get; set; }
  public DateTimeOffset? EndedAt { get; set; }
  public string? Text { get; set; }
  public string? ToolName { get; set; }
  public string? ToolUseId { get; set; }
  public string? ToolInput { get; set; }
  public string? ToolOutput { get; set; }
  public bool? Succeeded { get; set; }
  public Guid? ResponseId { get; set; }
  public Guid[] SourceEventIds { get; set; } = [];
  public Guid[] SourceLineIds { get; set; } = [];
}

public sealed class ModelResponseRow
{
  public Guid Id { get; set; }
  public Guid TurnId { get; set; }
  public Guid SessionId { get; set; }
  public string MessageId { get; set; } = "";
  public string Model { get; set; } = "";
  public DateTimeOffset RespondedAt { get; set; }
  public long InputTokens { get; set; }
  public long OutputTokens { get; set; }
  public long CacheWrite5mTokens { get; set; }
  public long CacheWrite1hTokens { get; set; }
  public long CacheReadTokens { get; set; }
  public long ThinkingTokens { get; set; }
  public long WebSearches { get; set; }
  public long WebFetches { get; set; }
  public Guid[] SourceEventIds { get; set; } = [];
  public Guid[] SourceLineIds { get; set; } = [];
}

// The projection worker's progress: one row.
public sealed class ProjectionStateRow
{
  public const string SessionsName = "sessions";

  public string Name { get; set; } = SessionsName;
  public DateTimeOffset? Watermark { get; set; }
  public int AppliedVersion { get; set; }
}
