using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using AgentLedger.Infrastructure.Data.Projections;
using AgentLedger.UseCases.Projections;

namespace AgentLedger.Infrastructure.Data.Stores;

// Writes projections a whole session at a time (ADR 0022): in one transaction, the session's turns are deleted (their
// steps and model responses go with them) and the new projection inserted, so readers see the old or the new session,
// never a mix. EF rather than SQL: a session is a few thousand rows, which EF batches, and the audit interceptor
// sets the timestamps.
public sealed class ProjectionStore(AppDbContext db) : IProjectionStore
{
  public async Task ReplaceSessionAsync(ProjectedSession session, int version, CancellationToken cancellationToken)
  {
    var sessionId = session.Id.Value;
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    await db.Turns.Where(t => t.SessionId == sessionId).ExecuteDeleteAsync(cancellationToken);

    var row = await db.Sessions.FindAsync([sessionId], cancellationToken);
    if (row is null)
    {
      row = new SessionRow { Id = sessionId };
      db.Sessions.Add(row);
    }
    row.Agent = session.Agent.Name;
    row.NativeSessionId = session.NativeSessionId;
    row.StartedAt = session.StartedAt;
    row.LastActivityAt = session.LastActivityAt;
    row.TurnCount = session.Turns.Count;
    row.ProjectionVersion = version;

    foreach (var (turn, sequence) in session.Turns.Select((t, i) => (t, i)))
    {
      db.Turns.Add(TurnRowOf(turn, sessionId, sequence));
      db.Steps.AddRange(turn.Steps.Select((s, i) => StepRowOf(s, turn.Id.Value, sessionId, i)));
      db.ModelResponses.AddRange(turn.Responses.Select(r => ResponseRowOf(r, turn.Id.Value, sessionId)));
    }

    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    db.ChangeTracker.Clear();
  }

  public async Task<IReadOnlyList<SessionKey>> ListOutdatedSessionsAsync(int version, int limit,
    CancellationToken cancellationToken)
  {
    var rows = await db.Sessions.AsNoTracking()
      .Where(s => s.ProjectionVersion < version)
      .OrderBy(s => s.LastActivityAt)
      .Take(limit)
      .Select(s => new { s.Agent, s.NativeSessionId })
      .ToListAsync(cancellationToken);
    return [.. rows.Select(r => new SessionKey(AgentKind.FromName(r.Agent, false), r.NativeSessionId))];
  }

  public async Task<ProjectionCheckpoint> GetCheckpointAsync(CancellationToken cancellationToken)
  {
    var row = await db.ProjectionStates.AsNoTracking()
      .SingleOrDefaultAsync(s => s.Name == ProjectionStateRow.SessionsName, cancellationToken);
    return row is null ? ProjectionCheckpoint.Initial : new ProjectionCheckpoint(row.Watermark, row.AppliedVersion);
  }

  public async Task SaveCheckpointAsync(ProjectionCheckpoint checkpoint, CancellationToken cancellationToken)
  {
    var row = await db.ProjectionStates.SingleOrDefaultAsync(s => s.Name == ProjectionStateRow.SessionsName, cancellationToken);
    if (row is null)
    {
      row = new ProjectionStateRow();
      db.ProjectionStates.Add(row);
    }
    row.Watermark = checkpoint.Watermark;
    row.AppliedVersion = checkpoint.AppliedVersion;
    await db.SaveChangesAsync(cancellationToken);
  }

  private static TurnRow TurnRowOf(Turn turn, Guid sessionId, int sequence) => new()
  {
    Id = turn.Id.Value,
    SessionId = sessionId,
    Sequence = sequence,
    PromptId = Storable(turn.PromptId),
    Status = turn.Status.Name,
    Source = turn.Source.Name,
    StartedAt = turn.StartedAt,
    EndedAt = turn.EndedAt,
    GitRepo = turn.Context.GitRepo,
    GitBranch = turn.Context.GitBranch,
    GitCommit = turn.Context.GitCommit,
    GitWorktree = turn.Context.GitWorktree,
    Directory = Storable(turn.Context.Directory),
    Host = turn.Context.Host,
    User = turn.Context.User,
    SourceEventIds = [.. turn.Lineage.EventIds.Select(e => e.Value)],
    SourceLineIds = [.. turn.Lineage.LineIds.Select(l => l.Value)],
  };

  private static StepRow StepRowOf(Step step, Guid turnId, Guid sessionId, int sequence) => new()
  {
    Id = step.Id.Value,
    TurnId = turnId,
    SessionId = sessionId,
    Sequence = sequence,
    Kind = step.Kind.Name,
    StartedAt = step.StartedAt,
    EndedAt = step.EndedAt,
    Text = Storable(step.Text),
    ToolName = Storable(step.ToolName),
    ToolUseId = Storable(step.ToolUseId),
    ToolInput = Storable(step.ToolInput),
    ToolOutput = Storable(step.ToolOutput),
    Succeeded = step.Succeeded,
    ResponseId = step.ResponseId?.Value,
    SourceEventIds = [.. step.Lineage.EventIds.Select(e => e.Value)],
    SourceLineIds = [.. step.Lineage.LineIds.Select(l => l.Value)],
  };

  private static ModelResponseRow ResponseRowOf(ModelResponse response, Guid turnId, Guid sessionId) => new()
  {
    Id = response.Id.Value,
    TurnId = turnId,
    SessionId = sessionId,
    MessageId = Storable(response.MessageId),
    Model = Storable(response.Model),
    RespondedAt = response.RespondedAt,
    InputTokens = response.Usage.Input,
    OutputTokens = response.Usage.Output,
    CacheWrite5mTokens = response.Usage.CacheWrite5m,
    CacheWrite1hTokens = response.Usage.CacheWrite1h,
    CacheReadTokens = response.Usage.CacheRead,
    ThinkingTokens = response.Usage.Thinking,
    WebSearches = response.Usage.WebSearches,
    WebFetches = response.Usage.WebFetches,
    SourceEventIds = [.. response.Lineage.EventIds.Select(e => e.Value)],
    SourceLineIds = [.. response.Lineage.LineIds.Select(l => l.Value)],
  };

  // Postgres text can't hold NUL, which decoded transcript text can contain (a "\u0000" escape in the raw line, which
  // is kept exactly). Projections show it as the replacement character.
  [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(text))]
  private static string? Storable(string? text) => text?.Replace('\0', '\uFFFD');
}
