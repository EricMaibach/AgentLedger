namespace AgentLedger.Core.Projections;

// One thing that happened inside a turn (ADR 0022), built by an agent's adapter from hook events and transcript
// lines. Created through the factory for its kind, which knows what that kind needs.
public sealed record Step
{
  public StepId Id { get; }
  public StepKind Kind { get; }
  public DateTimeOffset StartedAt { get; }
  public DateTimeOffset? EndedAt { get; }

  // Prompt, thinking or response text. Null for thinking without text (most thinking holds only a signature).
  public string? Text { get; }

  public string? ToolName { get; }
  public string? ToolUseId { get; }

  // The tool's input as JSON text, and its result as the model saw it.
  public string? ToolInput { get; }
  public string? ToolOutput { get; }

  // Null while a tool call has no result (interrupted or still running).
  public bool? Succeeded { get; }

  // The model response this step came from, if it did and that response is known.
  public ModelResponseId? ResponseId { get; }

  public Lineage Lineage { get; }

  private Step(StepId id, StepKind kind, DateTimeOffset startedAt, DateTimeOffset? endedAt, string? text, string? toolName,
    string? toolUseId, string? toolInput, string? toolOutput, bool? succeeded, ModelResponseId? responseId, Lineage lineage)
  {
    Id = id;
    Kind = kind;
    StartedAt = Guard.Against.Default(startedAt);
    if (endedAt < startedAt)
      throw new ArgumentException("A step can't end before it starts.", nameof(endedAt));
    EndedAt = endedAt;
    Text = text;
    ToolName = toolName;
    ToolUseId = toolUseId;
    ToolInput = toolInput;
    ToolOutput = toolOutput;
    Succeeded = succeeded;
    ResponseId = responseId;
    Lineage = Guard.Against.Null(lineage);
  }

  public static Step Prompt(StepId id, DateTimeOffset at, string text, Lineage lineage) =>
    new(id, StepKind.Prompt, at, null, Guard.Against.NullOrWhiteSpace(text), null, null, null, null, null, null, lineage);

  public static Step Thinking(StepId id, DateTimeOffset at, string? text, ModelResponseId? responseId, Lineage lineage) =>
    new(id, StepKind.Thinking, at, null, string.IsNullOrWhiteSpace(text) ? null : text, null, null, null, null, null,
      responseId, lineage);

  public static Step Response(StepId id, DateTimeOffset at, string text, ModelResponseId? responseId, Lineage lineage) =>
    new(id, StepKind.Response, at, null, Guard.Against.NullOrWhiteSpace(text), null, null, null, null, null, responseId,
      lineage);

  public static Step ToolCall(StepId id, DateTimeOffset at, string toolName, string toolUseId, string? input, string? output,
    bool? succeeded, DateTimeOffset? endedAt, ModelResponseId? responseId, Lineage lineage) =>
    new(id, StepKind.ToolCall, at, endedAt, null, Guard.Against.NullOrWhiteSpace(toolName),
      Guard.Against.NullOrWhiteSpace(toolUseId), input, output, succeeded, responseId, lineage);
}
