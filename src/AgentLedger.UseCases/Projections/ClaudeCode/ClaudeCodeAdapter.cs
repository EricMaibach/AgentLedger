using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;

namespace AgentLedger.UseCases.Projections.ClaudeCode;

// Claude Code's adapter (ADR 0022): merges its hook events and transcript into turns and steps. A turn is one
// prompt_id (hooks) / promptId (transcript). The transcript decides content and order; hooks add timing, failures and
// evidence, and stand in where the transcript has nothing.
public sealed class ClaudeCodeAdapter : IAgentAdapter
{
  private const string SyntheticModel = "<synthetic>";
  private static readonly string[] ToolEventTypes = ["PreToolUse", "PostToolUse", "PostToolUseFailure"];

  public AgentKind Agent => AgentKind.ClaudeCode;

  public ProjectedSession Project(SessionEvidence evidence)
  {
    ArgumentNullException.ThrowIfNull(evidence);
    var transcript = ClaudeCodeTranscript.Read(evidence.Lines);
    // A subagent's events carry its launching turn's prompt_id and its own agent_id: they become the subagent's
    // nested steps (build step 3), not steps of the turn.
    var hooks = HookEvent.Read(evidence.Receipts).Where(h => h.AgentId is null).ToList();

    var promptIds = transcript.Prompts.Select(p => p.PromptId)
      .Concat(transcript.Blocks.Select(b => b.PromptId).OfType<string>())
      .Concat(hooks.Select(h => h.PromptId).OfType<string>())
      .Distinct();
    var drafts = promptIds
      .Select(id => Draft(evidence.NativeSessionId, id, transcript, hooks))
      .OfType<TurnDraft>()
      .OrderBy(d => d.StartedAt)
      .ToList();
    var turns = drafts.Select((d, i) => d.ToTurn(evidence.NativeSessionId, isLast: i == drafts.Count - 1)).ToList();

    var times = transcript.Times.Concat(hooks.Select(h => h.At)).ToList();
    if (times.Count == 0)
      times = [.. evidence.Receipts.Select(r => r.ReceivedAt).Concat(evidence.Lines.Select(l => l.ReceivedAt))];
    if (times.Count == 0)
      throw new ArgumentException("A session's evidence can't be empty.", nameof(evidence));
    var title = transcript.AiTitle ?? turns.FirstOrDefault()?.Steps.FirstOrDefault(s => s.Kind == StepKind.Prompt)?.Text;
    return new ProjectedSession(Agent, evidence.NativeSessionId, times.Min(), times.Max(), turns, title);
  }

  private TurnDraft? Draft(string sessionId, string promptId, ClaudeCodeTranscript transcript, IReadOnlyList<HookEvent> allHooks)
  {
    var hooks = allHooks.Where(h => h.PromptId == promptId).ToList();
    var blocks = transcript.Blocks.Where(b => b.PromptId == promptId).ToList();
    var steps = new List<(Step Step, int Order)>();

    var promptLine = transcript.Prompts.FirstOrDefault(p => p.PromptId == promptId);
    var promptEvents = hooks.Where(h => h.Type == "UserPromptSubmit").ToList();
    var promptText = promptLine?.Text ?? promptEvents.Select(h => h.Prompt).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
    if (promptText is not null)
    {
      var at = new[] { promptLine?.At }.Concat(promptEvents.Select(h => (DateTimeOffset?)h.At)).OfType<DateTimeOffset>().Min();
      steps.Add((Step.Prompt(StepId(sessionId, StepKind.Prompt, promptId), at, promptText,
        Sources(promptEvents, promptLine is null ? [] : [promptLine.Line])), promptLine?.Order ?? 0));
    }

    var responses = Responses(sessionId, blocks);
    for (var i = 0; i < blocks.Count; i++)
    {
      var block = blocks[i];
      var responseId = block.Model == SyntheticModel ? (ModelResponseId?)null : ModelResponseId.For(Agent, sessionId, block.MessageId);
      var key = $"{block.Line.Id.Value}#{block.Index}";
      var lineage = Sources([], [block.Line]);
      switch (block.BlockType)
      {
        // With summaries on, Claude Code writes a thinking block twice: without text, then with its summary. One step,
        // keeping the first line's key so the ID doesn't change when the summary arrives.
        case "thinking" when IsSummaryOf(block, i + 1 < blocks.Count ? blocks[i + 1] : null):
          var summary = blocks[++i];
          steps.Add((Step.Thinking(StepId(sessionId, StepKind.Thinking, key), block.At, summary.Text, responseId,
            Sources([], [block.Line, summary.Line])), block.Order));
          break;
        case "thinking":
          steps.Add((Step.Thinking(StepId(sessionId, StepKind.Thinking, key), block.At, block.Text, responseId, lineage), block.Order));
          break;
        case "text" when !string.IsNullOrWhiteSpace(block.Text):
          steps.Add((Step.Response(StepId(sessionId, StepKind.Response, key), block.At, block.Text, responseId, lineage), block.Order));
          break;
      }
    }

    var toolUseIds = blocks.Where(b => b.BlockType == "tool_use").Select(b => b.ToolUseId)
      .Concat(hooks.Where(h => ToolEventTypes.Contains(h.Type)).Select(h => h.ToolUseId))
      .OfType<string>().Distinct();
    foreach (var toolUseId in toolUseIds)
    {
      var block = blocks.FirstOrDefault(b => b.BlockType == "tool_use" && b.ToolUseId == toolUseId);
      // By tool_use_id across the session: events from older Claude Code versions have no prompt_id.
      var toolHooks = allHooks.Where(h => h.ToolUseId == toolUseId && ToolEventTypes.Contains(h.Type)).ToList();
      var result = transcript.Results.FirstOrDefault(r => r.ToolUseId == toolUseId);
      if (ToolCall(sessionId, toolUseId, block, toolHooks, result) is { } call)
        steps.Add((call, block?.Order ?? result?.Order ?? int.MaxValue));
    }

    if (steps.Count == 0)
      return null;

    var ordered = steps.OrderBy(s => s.Step.StartedAt).ThenBy(s => s.Order).Select(s => s.Step).ToList();
    var lastActivity = ordered.SelectMany(s => new[] { s.StartedAt, s.EndedAt ?? s.StartedAt })
      .Concat(hooks.Select(h => h.At)).Concat(responses.Select(r => r.RespondedAt)).Max();
    return new TurnDraft(Agent, promptId, Source(promptLine?.Source), ordered[0].StartedAt, lastActivity, ordered, responses,
      Context(hooks, promptLine), hooks, blocks);
  }

  private static bool IsSummaryOf(AssistantBlock block, AssistantBlock? next) =>
    string.IsNullOrWhiteSpace(block.Text) && next is { BlockType: "thinking" } && next.MessageId == block.MessageId
    && !string.IsNullOrWhiteSpace(next.Text);

  private Step? ToolCall(string sessionId, string toolUseId, AssistantBlock? block, List<HookEvent> hooks,
    ToolResultEntry? result)
  {
    var pre = hooks.FirstOrDefault(h => h.Type == "PreToolUse");
    var post = hooks.FirstOrDefault(h => h.Type == "PostToolUse");
    var failure = hooks.FirstOrDefault(h => h.Type == "PostToolUseFailure");
    var name = block?.ToolName ?? hooks.Select(h => h.ToolName).FirstOrDefault(n => n is not null);
    if (name is null)
      return null;

    var startedAt = block?.At ?? hooks.Min(h => h.At);
    DateTimeOffset? endedAt = failure?.At ?? post?.At ?? result?.At;
    if (endedAt < startedAt)
      endedAt = startedAt;
    var output = result?.Output ?? post?.ToolResponse ?? failure?.Error;
    bool? succeeded = failure is not null || result?.IsError == true ? false
      : result is not null || post is not null ? true
      : null;
    var input = block?.ToolInput ?? pre?.ToolInput ?? post?.ToolInput ?? failure?.ToolInput;
    var responseId = block is null || block.Model == SyntheticModel ? (ModelResponseId?)null
      : ModelResponseId.For(Agent, sessionId, block.MessageId);
    var lines = new[] { block?.Line, result?.Line }.OfType<Core.TranscriptAggregate.TranscriptLine>();
    return Step.ToolCall(StepId(sessionId, StepKind.ToolCall, toolUseId), startedAt, name, toolUseId, input, output, succeeded,
      endedAt, responseId, Sources(hooks, lines), ClaudeCodeToolSummary.Of(name, input));
  }

  // One response per message ID; its tokens from its last line, since a line written while streaming can carry lower
  // counts (ADR 0022). Claude Code's own synthetic messages made no API call.
  private List<ModelResponse> Responses(string sessionId, List<AssistantBlock> blocks) =>
    blocks.Where(b => b.Model != SyntheticModel)
      .GroupBy(b => b.MessageId)
      .Select(g =>
      {
        var last = g.OrderBy(b => b.Order).Last();
        return new ModelResponse(ModelResponseId.For(Agent, sessionId, g.Key), g.Key, last.Model, g.Min(b => b.At), last.Usage,
          Sources([], g.Select(b => b.Line)));
      })
      .ToList();

  private static PromptSource Source(string? source) => source switch
  {
    "queued" => PromptSource.Queued,
    "system" => PromptSource.Injected,
    _ => PromptSource.Typed,
  };

  // Where the prompt was sent from: the CLI's capture context when there's an event, else what the transcript records.
  private static TurnContext Context(List<HookEvent> hooks, PromptEntry? prompt)
  {
    var receipt = (hooks.FirstOrDefault(h => h.Type == "UserPromptSubmit") ?? hooks.FirstOrDefault())?.Receipt;
    if (receipt is not null)
    {
      var c = receipt.Context;
      return new TurnContext(c.GitRepo, c.GitBranch, c.GitCommit, c.GitWorktree, c.ProjectDir, c.Host, c.User);
    }
    return TurnContext.None with { GitBranch = prompt?.GitBranch, Directory = prompt?.Directory };
  }

  private StepId StepId(string sessionId, StepKind kind, string key) => Core.Projections.StepId.For(Agent, sessionId, kind, key);

  private static Lineage Sources(IEnumerable<HookEvent> hooks, IEnumerable<Core.TranscriptAggregate.TranscriptLine> lines) =>
    new(hooks.Select(h => h.EventId), lines.Select(l => l.Id));

  private sealed record TurnDraft(AgentKind Agent, string PromptId, PromptSource Source, DateTimeOffset StartedAt,
    DateTimeOffset LastActivity, List<Step> Steps, List<ModelResponse> Responses, TurnContext Context, List<HookEvent> Hooks,
    List<AssistantBlock> Blocks)
  {
    // Stop and StopFailure say how a turn ended. Without them: the model's own end of turn, or a turn that never
    // called the model (a local command), is complete; a later prompt interrupted it; otherwise it's still running.
    public Turn ToTurn(string sessionId, bool isLast)
    {
      var failure = Hooks.LastOrDefault(h => h.Type == "StopFailure");
      var stop = Hooks.LastOrDefault(h => h.Type == "Stop");
      var lastBlock = Blocks.OrderBy(b => b.Order).LastOrDefault();
      var hasModelActivity = Blocks.Count > 0 || Steps.Any(s => s.Kind == StepKind.ToolCall);
      (TurnStatus status, DateTimeOffset? end) = (failure, stop) switch
      {
        ({ } f, _) => (TurnStatus.Failed, f.At),
        (_, { } s) => (TurnStatus.Completed, s.At),
        _ when lastBlock?.StopReason == "end_turn" => (TurnStatus.Completed, LastActivity),
        _ when !hasModelActivity => (TurnStatus.Completed, LastActivity),
        _ when !isLast => (TurnStatus.Interrupted, LastActivity),
        _ => (TurnStatus.InProgress, (DateTimeOffset?)null),
      };
      if (end < StartedAt)
        end = StartedAt;
      var ending = new[] { failure, stop }.OfType<HookEvent>().Select(h => h.EventId).ToList();
      return new Turn(TurnId.For(Agent, sessionId, PromptId), PromptId, Source, status, StartedAt, end, Context, Steps,
        Responses, ending.Count == 0 ? null : new Lineage(ending, []));
    }
  }
}
