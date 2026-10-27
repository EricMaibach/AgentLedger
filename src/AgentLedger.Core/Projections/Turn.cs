using AgentLedger.Core.AgentEventReceiptAggregate;

namespace AgentLedger.Core.Projections;

// Where a turn's prompt was sent from: the git context and folder at that moment (ADR 0022). Every part is optional:
// a turn built from transcript lines alone knows only what the transcript records.
public sealed record TurnContext(
  string? GitRepo,
  string? GitBranch,
  string? GitCommit,
  string? GitWorktree,
  string? Directory,
  string? Host,
  string? User)
{
  public static readonly TurnContext None = new(null, null, null, null, null, null, null);
}

// One prompt and everything the agent did for it, until it stopped (ADR 0022). The smallest unit assigned to a
// branch, commit or release; never split.
public sealed record Turn
{
  public TurnId Id { get; }
  public string PromptId { get; }
  public PromptSource Source { get; }
  public TurnStatus Status { get; }
  public DateTimeOffset StartedAt { get; }

  // Null exactly while the turn is in progress.
  public DateTimeOffset? EndedAt { get; }

  public TurnContext Context { get; }
  public IReadOnlyList<Step> Steps { get; }
  public IReadOnlyList<ModelResponse> Responses { get; }

  // Every source of its steps and responses, and the evidence of how it ended.
  public Lineage Lineage { get; }

  // ending: the evidence that decided its status without being part of a step (e.g. the agent's stop event).
  public Turn(TurnId id, string promptId, PromptSource source, TurnStatus status, DateTimeOffset startedAt,
    DateTimeOffset? endedAt, TurnContext context, IEnumerable<Step> steps, IEnumerable<ModelResponse> responses,
    Lineage? ending = null)
  {
    Id = id;
    PromptId = Guard.Against.NullOrWhiteSpace(promptId);
    Source = Guard.Against.Null(source);
    Status = Guard.Against.Null(status);
    StartedAt = Guard.Against.Default(startedAt);
    if ((status == TurnStatus.InProgress) != (endedAt is null))
      throw new ArgumentException("A turn has an end exactly when it's no longer in progress.", nameof(endedAt));
    if (endedAt < startedAt)
      throw new ArgumentException("A turn can't end before it starts.", nameof(endedAt));
    EndedAt = endedAt;
    Context = Guard.Against.Null(context);

    Steps = [.. Guard.Against.Null(steps)];
    Responses = [.. Guard.Against.Null(responses)];
    if (Steps.Count == 0)
      throw new ArgumentException("A turn has at least one step.", nameof(steps));
    if (Steps.Select(s => s.Id).Distinct().Count() != Steps.Count)
      throw new ArgumentException("A turn's steps are unique.", nameof(steps));
    if (Responses.Select(r => r.Id).Distinct().Count() != Responses.Count)
      throw new ArgumentException("A turn's model responses are unique.", nameof(responses));
    var responseIds = Responses.Select(r => r.Id).ToHashSet();
    if (Steps.Any(s => s.ResponseId is { } r && !responseIds.Contains(r)))
      throw new ArgumentException("A step refers to a model response the turn doesn't have.", nameof(steps));

    Lineage = Lineage.Combine(Steps.Select(s => s.Lineage).Concat(Responses.Select(r => r.Lineage))
      .Concat(ending is null ? [] : [ending]));
  }
}

// One agent conversation, projected (ADR 0022): its turns in order. Its end is inferred from its last activity
// (ADR 0004).
public sealed record ProjectedSession
{
  public SessionId Id { get; }
  public AgentKind Agent { get; }
  public string NativeSessionId { get; }
  public DateTimeOffset StartedAt { get; }
  public DateTimeOffset LastActivityAt { get; }
  public IReadOnlyList<Turn> Turns { get; }

  // A short name for the session, when its agent gives one or the adapter can derive one.
  public string? Title { get; }

  public ProjectedSession(AgentKind agent, string nativeSessionId, DateTimeOffset startedAt, DateTimeOffset lastActivityAt,
    IEnumerable<Turn> turns, string? title = null)
  {
    Agent = Guard.Against.Null(agent);
    NativeSessionId = Guard.Against.NullOrWhiteSpace(nativeSessionId);
    Id = SessionId.For(Agent, NativeSessionId);
    StartedAt = Guard.Against.Default(startedAt);
    if (lastActivityAt < startedAt)
      throw new ArgumentException("A session's last activity can't be before its start.", nameof(lastActivityAt));
    LastActivityAt = lastActivityAt;
    Turns = [.. Guard.Against.Null(turns)];
    if (Turns.Select(t => t.Id).Distinct().Count() != Turns.Count)
      throw new ArgumentException("A session's turns are unique.", nameof(turns));
    Title = Step.FirstLine(title);
  }
}
