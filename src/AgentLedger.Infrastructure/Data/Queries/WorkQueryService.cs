using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.Infrastructure.Data.Projections;
using AgentLedger.UseCases.Work;

namespace AgentLedger.Infrastructure.Data.Queries;

// Reads projections for the work pages (ADR 0023): sessions, turns, steps, and a step's evidence from the raw tables.
public sealed class WorkQueryService(AppDbContext db) : IWorkQueryService
{
  private const string ToolCall = "ToolCall";

  public async Task<SessionPage> ListSessionsAsync(SessionCursor? after, int limit, CancellationToken cancellationToken)
  {
    var sessions = db.Sessions.AsNoTracking();
    if (after is not null)
    {
      sessions = sessions.Where(s => s.LastActivityAt < after.LastActivityAt
                                     || (s.LastActivityAt == after.LastActivityAt && s.Id.CompareTo(after.SessionId) < 0));
    }

    var rows = await sessions.OrderByDescending(s => s.LastActivityAt).ThenByDescending(s => s.Id)
      .Take(limit + 1) // one extra row says whether another page exists
      .ToListAsync(cancellationToken);
    var items = rows.Take(limit).Select(ListItem).ToList();
    var next = rows.Count > limit ? new SessionCursor(items[^1].LastActivityAt, items[^1].SessionId) : null;
    return new SessionPage(items, next);
  }

  public async Task<SessionDetail?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
  {
    var row = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    if (row is null)
      return null;

    var statuses = await db.Turns.Where(t => t.SessionId == sessionId).GroupBy(t => t.Status)
      .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
    var tools = await db.Steps.Where(s => s.SessionId == sessionId && s.Kind == ToolCall)
      .GroupBy(_ => 1)
      .Select(g => new { All = g.Count(), Failed = g.Count(s => s.Succeeded == false) })
      .SingleOrDefaultAsync(cancellationToken);
    var tokens = await db.ModelResponses.Where(r => r.SessionId == sessionId).GroupBy(r => r.Model)
      .Select(g => new ModelTokens(g.Key, g.Count(), g.Sum(r => r.InputTokens), g.Sum(r => r.OutputTokens),
        g.Sum(r => r.CacheWrite5mTokens + r.CacheWrite1hTokens), g.Sum(r => r.CacheReadTokens), g.Sum(r => r.ThinkingTokens)))
      .ToListAsync(cancellationToken);

    return new SessionDetail(ListItem(row), row.Host, row.User, statuses.ToDictionary(s => s.Status, s => s.Count),
      tools?.All ?? 0, tools?.Failed ?? 0, [.. tokens.OrderByDescending(t => t.Output)]);
  }

  public async Task<IReadOnlyList<TurnListItem>?> ListTurnsAsync(Guid sessionId, CancellationToken cancellationToken)
  {
    if (!await db.Sessions.AnyAsync(s => s.Id == sessionId, cancellationToken))
      return null;

    return await db.Turns.AsNoTracking().Where(t => t.SessionId == sessionId).OrderBy(t => t.Sequence)
      .Select(t => new TurnListItem(t.Id, t.Sequence + 1, t.Status, t.Source, t.StartedAt, t.EndedAt,
        db.Steps.Where(s => s.TurnId == t.Id && s.Kind == "Prompt").Select(s => s.Summary).FirstOrDefault(),
        db.Steps.Count(s => s.TurnId == t.Id && s.Kind == ToolCall),
        db.Steps.Count(s => s.TurnId == t.Id && s.Kind == ToolCall && s.Succeeded == false)))
      .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<StepListItem>?> ListStepsAsync(Guid turnId, CancellationToken cancellationToken)
  {
    if (!await db.Turns.AnyAsync(t => t.Id == turnId, cancellationToken))
      return null;

    return await db.Steps.AsNoTracking().Where(s => s.TurnId == turnId).OrderBy(s => s.Sequence)
      .Select(s => new StepListItem(s.Id, s.Kind, s.StartedAt, s.EndedAt, s.Summary, s.ToolName, s.Succeeded))
      .ToListAsync(cancellationToken);
  }

  public async Task<StepDetail?> GetStepAsync(Guid stepId, CancellationToken cancellationToken)
  {
    var found = await (
        from step in db.Steps.AsNoTracking()
        join turn in db.Turns on step.TurnId equals turn.Id
        where step.Id == stepId
        select new { Step = step, Turn = turn })
      .SingleOrDefaultAsync(cancellationToken);
    if (found is null)
      return null;

    var (s, t) = (found.Step, found.Turn);
    var response = s.ResponseId is { } responseId
      ? await db.ModelResponses.AsNoTracking().Where(r => r.Id == responseId)
        .Select(r => new ResponseTokens(r.Model, r.InputTokens, r.OutputTokens, r.CacheWrite5mTokens, r.CacheWrite1hTokens,
          r.CacheReadTokens, r.ThinkingTokens))
        .SingleOrDefaultAsync(cancellationToken)
      : null;

    return new StepDetail(s.Id, s.Kind, s.StartedAt, s.EndedAt, s.Text, s.Summary, s.ToolName, s.ToolUseId, s.ToolInput, s.ToolOutput,
      s.Succeeded, new WorkLocation(t.SessionId, t.Id), t.Sequence + 1, t.StartedAt, t.Status, t.Source, response,
      await EvidenceEventsAsync(s, cancellationToken), await EvidenceLinesAsync(s, cancellationToken));
  }

  public Task<WorkLocation?> LocateTurnAsync(Guid turnId, CancellationToken cancellationToken) =>
    db.Turns.AsNoTracking().Where(t => t.Id == turnId)
      .Select(t => new WorkLocation(t.SessionId, t.Id)).SingleOrDefaultAsync(cancellationToken);

  public Task<WorkLocation?> LocateStepAsync(Guid stepId, CancellationToken cancellationToken) =>
    db.Steps.AsNoTracking().Where(s => s.Id == stepId)
      .Select(s => new WorkLocation(s.SessionId, s.TurnId)).SingleOrDefaultAsync(cancellationToken);

  // Each event once, described by its first receipt; in the order stored.
  private async Task<List<EvidenceEvent>> EvidenceEventsAsync(StepRow step, CancellationToken cancellationToken)
  {
    var eventIds = step.SourceEventIds.Select(Core.AgentEventReceiptAggregate.AgentEventId.From).ToList();
    var receipts = await db.AgentEventReceipts.AsNoTracking().Where(r => eventIds.Contains(r.EventId))
      .Select(r => new { r.EventId, r.EventType, r.CapturedAt, r.ReceivedAt }).ToListAsync(cancellationToken);
    return [.. step.SourceEventIds.Select(id =>
    {
      var first = receipts.Where(r => r.EventId.Value == id).OrderBy(r => r.ReceivedAt).FirstOrDefault();
      return new EvidenceEvent(id, first?.EventType, first?.CapturedAt);
    })];
  }

  private async Task<List<EvidenceLine>> EvidenceLinesAsync(StepRow step, CancellationToken cancellationToken)
  {
    var lineIds = step.SourceLineIds.Select(TranscriptLineId.From).ToList();
    var lines = await db.TranscriptLines.AsNoTracking().Where(l => lineIds.Contains(l.Id))
      .Select(l => new { l.Id, l.LineType, l.File, l.ByteOffset }).ToListAsync(cancellationToken);
    return [.. step.SourceLineIds.Select(id =>
    {
      var line = lines.SingleOrDefault(l => l.Id.Value == id);
      return new EvidenceLine(id, line?.LineType, line?.File.Value, line?.ByteOffset);
    })];
  }

  private static SessionListItem ListItem(SessionRow s) =>
    new(s.Id, s.Agent, s.NativeSessionId, s.Title, s.GitRepo, s.GitBranch, s.TurnCount, s.StartedAt, s.LastActivityAt);
}
