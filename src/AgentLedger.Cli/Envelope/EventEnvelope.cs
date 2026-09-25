namespace AgentLedger.Cli.Envelope;

/// <summary>What the CLI sends to <c>POST /events</c> (ADR 0011). <see cref="Payload"/> is the hook's stdin, unmodified.</summary>
internal sealed record EventEnvelope(
  Guid EventId,
  string Agent,
  string EventType,
  string? NativeSessionId,
  DateTimeOffset CapturedAt,
  string Host,
  string User,
  string ProjectDir,
  string? GitRepo,
  string? GitBranch,
  string? GitWorktree,
  IReadOnlyDictionary<string, string> Tags,
  string Payload);
