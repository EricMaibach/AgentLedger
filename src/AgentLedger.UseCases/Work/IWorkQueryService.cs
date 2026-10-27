namespace AgentLedger.UseCases.Work;

// Reads the projections for the work pages and, later, the MCP server (ADR 0022, ADR 0023). Plain DTOs: the
// projection model's names (kinds, statuses, sources) as strings, IDs as Guids.
public interface IWorkQueryService
{
  // Most recently active first; keyset paged on (last activity, ID).
  Task<SessionPage> ListSessionsAsync(SessionCursor? after, int limit, CancellationToken cancellationToken);

  Task<SessionDetail?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);

  // Every turn of a session, oldest first; null when the session doesn't exist.
  Task<IReadOnlyList<TurnListItem>?> ListTurnsAsync(Guid sessionId, CancellationToken cancellationToken);

  // A turn's steps in order; null when the turn doesn't exist.
  Task<IReadOnlyList<StepListItem>?> ListStepsAsync(Guid turnId, CancellationToken cancellationToken);

  Task<StepDetail?> GetStepAsync(Guid stepId, CancellationToken cancellationToken);

  Task<WorkLocation?> LocateTurnAsync(Guid turnId, CancellationToken cancellationToken);

  Task<WorkLocation?> LocateStepAsync(Guid stepId, CancellationToken cancellationToken);
}

public sealed record SessionCursor(DateTimeOffset LastActivityAt, Guid SessionId);

public sealed record SessionPage(IReadOnlyList<SessionListItem> Items, SessionCursor? Next);

public sealed record SessionListItem(Guid SessionId, string Agent, string NativeSessionId, string? Title, string? GitRepo,
  string? GitBranch, int TurnCount, DateTimeOffset StartedAt, DateTimeOffset LastActivityAt)
{
  // Set by the query handler, which knows the time.
  public bool Active { get; init; }
}

public sealed record SessionDetail(SessionListItem Session, string? Host, string? User, IReadOnlyDictionary<string, int> TurnsByStatus,
  int ToolCalls, int FailedToolCalls, IReadOnlyList<ModelTokens> TokensByModel);

public sealed record ModelTokens(string Model, int Responses, long Input, long Output, long CacheWrite, long CacheRead, long Thinking);

public sealed record TurnListItem(Guid TurnId, int Number, string Status, string Source, DateTimeOffset StartedAt,
  DateTimeOffset? EndedAt, string? PromptSummary, int ToolCalls, int FailedToolCalls);

public sealed record StepListItem(Guid StepId, string Kind, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? Summary,
  string? ToolName, bool? Succeeded);

public sealed record StepDetail(Guid StepId, string Kind, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? Text,
  string? Summary, string? ToolName, string? ToolUseId, string? ToolInput, string? ToolOutput, bool? Succeeded,
  WorkLocation Location, int TurnNumber, DateTimeOffset TurnStartedAt, string TurnStatus, string TurnSource,
  ResponseTokens? Response, IReadOnlyList<EvidenceEvent> EvidenceEvents, IReadOnlyList<EvidenceLine> EvidenceLines);

public sealed record ResponseTokens(string Model, long Input, long Output, long CacheWrite5m, long CacheWrite1h, long CacheRead,
  long Thinking);

// The raw sources of a projected item (ADR 0022): hook events by event ID (with the first receipt's type and capture
// time, when it's stored) and transcript lines.
public sealed record EvidenceEvent(Guid EventId, string? EventType, DateTimeOffset? CapturedAt);

public sealed record EvidenceLine(Guid LineId, string? LineType, string? File, long? ByteOffset);

public sealed record WorkLocation(Guid SessionId, Guid? TurnId);
