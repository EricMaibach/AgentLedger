namespace AgentLedger.Core.Projections;

// What a step inside a turn is (ADR 0022). Subagent, context and event steps come with build step 3.
public sealed class StepKind : SmartEnum<StepKind>
{
  public static readonly StepKind Prompt = new(nameof(Prompt), 1);
  public static readonly StepKind Thinking = new(nameof(Thinking), 2);
  public static readonly StepKind Response = new(nameof(Response), 3);
  public static readonly StepKind ToolCall = new(nameof(ToolCall), 4);

  private StepKind(string name, int value) : base(name, value) { }
}

public sealed class TurnStatus : SmartEnum<TurnStatus>
{
  public static readonly TurnStatus Completed = new(nameof(Completed), 1);
  public static readonly TurnStatus Failed = new(nameof(Failed), 2);

  // The next prompt arrived without the agent stopping (e.g. the user pressed Esc).
  public static readonly TurnStatus Interrupted = new(nameof(Interrupted), 3);
  public static readonly TurnStatus InProgress = new(nameof(InProgress), 4);

  private TurnStatus(string name, int value) : base(name, value) { }
}

// Where a turn's prompt came from: injected prompts (hook output, task notifications) aren't a person's words.
public sealed class PromptSource : SmartEnum<PromptSource>
{
  public static readonly PromptSource Typed = new(nameof(Typed), 1);

  // Typed while the agent was busy, delivered when it was ready.
  public static readonly PromptSource Queued = new(nameof(Queued), 2);
  public static readonly PromptSource Injected = new(nameof(Injected), 3);

  private PromptSource(string name, int value) : base(name, value) { }
}
