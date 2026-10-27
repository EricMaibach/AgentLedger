using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UnitTests.Core.Projections;

// Valid projection pieces for tests, which change only what they're about.
internal static class Projected
{
  public const string Session = "3829bce8-470e-43d0-a99e-5495558d1976";
  public static readonly DateTimeOffset T0 = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);

  public static Lineage Lineage(int seed = 1) =>
    new([AgentEventId.From(new Guid(seed, 0, 0, new byte[8]))], []);

  public static StepId StepId(StepKind kind, string key) => AgentLedger.Core.Projections.StepId.For(AgentKind.ClaudeCode, Session, kind, key);

  public static ModelResponseId ResponseId(string messageId = "msg_01") =>
    ModelResponseId.For(AgentKind.ClaudeCode, Session, messageId);

  public static ModelResponse Response(string messageId = "msg_01") =>
    new(ResponseId(messageId), messageId, "claude-opus-5-5", T0.AddSeconds(2), new TokenUsage(3, 120, 0, 50, 4000, 10, 0, 0),
      Lineage(7));

  public static Step Prompt(string promptId = "p-1") =>
    Step.Prompt(StepId(StepKind.Prompt, promptId), T0, "Add a test", Lineage());

  public static TurnBuilder Turn(string promptId = "p-1") => new(promptId);
}

internal sealed class TurnBuilder(string promptId)
{
  private TurnStatus _status = TurnStatus.Completed;
  private DateTimeOffset? _endedAt = Projected.T0.AddMinutes(1);
  private List<Step> _steps = [Projected.Prompt(promptId)];
  private List<ModelResponse> _responses = [];
  private Lineage? _ending;

  public TurnBuilder InProgress() { _status = TurnStatus.InProgress; _endedAt = null; return this; }
  public TurnBuilder WithStatus(TurnStatus status, DateTimeOffset? endedAt) { _status = status; _endedAt = endedAt; return this; }
  public TurnBuilder WithSteps(params Step[] steps) { _steps = [.. steps]; return this; }
  public TurnBuilder WithResponses(params ModelResponse[] responses) { _responses = [.. responses]; return this; }
  public TurnBuilder WithEnding(Lineage ending) { _ending = ending; return this; }

  public Turn Build() =>
    new(TurnId.For(AgentKind.ClaudeCode, Projected.Session, promptId), promptId, PromptSource.Typed, _status, Projected.T0,
      _endedAt, TurnContext.None, _steps, _responses, _ending);
}
