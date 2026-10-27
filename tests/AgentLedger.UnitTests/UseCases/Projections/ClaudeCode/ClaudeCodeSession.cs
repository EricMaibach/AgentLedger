using System.Text.Json;
using System.Text.Json.Nodes;
using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.Projections;
using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.UseCases.Projections;
using AgentLedger.UseCases.Projections.ClaudeCode;

namespace AgentLedger.UnitTests.UseCases.Projections.ClaudeCode;

// Builds one Claude Code session's raw evidence the way Claude Code writes it (shapes copied from real transcripts and
// hook payloads, 2026-10-26), with made-up content: no real conversation text in the repository.
// Times are seconds after T0.
internal sealed class ClaudeCodeSession
{
  public const string Id = "3829bce8-470e-43d0-a99e-5495558d1976";
  public static readonly DateTimeOffset T0 = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
  public static readonly CaptureContext Context =
    new("devbox", "eric", "/workspace", "github.com/acme/shop", "main", "/workspace", new string('a', 40));

  private static readonly ApiKeyId Key = ApiKeyId.From(Guid.Parse("0192f0aa-0000-7000-8000-0000000000ff"));
  private readonly List<TranscriptLine> _lines = [];
  private readonly List<AgentEventReceipt> _receipts = [];
  private long _offset;
  private string? _lastUuid;

  public static DateTimeOffset At(double second) => T0.AddSeconds(second);

  // Transcript lines

  public ClaudeCodeSession PromptLine(string promptId, string text, double at, string? source = "typed",
    bool isMeta = false, bool isCompactSummary = false)
  {
    var line = UserLine(promptId, at);
    line["message"] = new JsonObject { ["role"] = "user", ["content"] = text };
    if (source is not null) line["promptSource"] = source;
    if (isMeta) line["isMeta"] = true;
    if (isCompactSummary) line["isCompactSummary"] = true;
    return Line(line);
  }

  public ClaudeCodeSession Thinking(string messageId, double at, string text = "", Usage? usage = null) =>
    Assistant(messageId, at, new JsonObject { ["type"] = "thinking", ["thinking"] = text, ["signature"] = "c2ln" }, usage);

  public ClaudeCodeSession Text(string messageId, double at, string text, Usage? usage = null, string stopReason = "end_turn",
    string model = "claude-opus-5-5") =>
    Assistant(messageId, at, new JsonObject { ["type"] = "text", ["text"] = text }, usage, stopReason, model);

  public ClaudeCodeSession ToolUse(string messageId, double at, string toolUseId, string tool = "Bash",
    string input = """{"command":"ls"}""", Usage? usage = null) =>
    Assistant(messageId, at,
      new JsonObject { ["type"] = "tool_use", ["id"] = toolUseId, ["name"] = tool, ["input"] = JsonNode.Parse(input) },
      usage, "tool_use");

  public ClaudeCodeSession ToolResult(string promptId, string toolUseId, double at, string content = "a.txt",
    bool? isError = false, bool contentAsBlocks = false)
  {
    var result = new JsonObject
    {
      ["type"] = "tool_result",
      ["tool_use_id"] = toolUseId,
      ["content"] = contentAsBlocks
        ? new JsonArray(new JsonObject { ["type"] = "text", ["text"] = content },
            new JsonObject { ["type"] = "tool_reference", ["tool_name"] = "Read" })
        : content,
    };
    if (isError is not null) result["is_error"] = isError;
    var line = UserLine(promptId, at);
    line["message"] = new JsonObject { ["role"] = "user", ["content"] = new JsonArray(result) };
    return Line(line);
  }

  public ClaudeCodeSession RawLine(string text, string file = "main")
  {
    _lines.Add(new TranscriptLine(AgentKind.ClaudeCode, Id, TranscriptFileName.From(file), _offset, text, Key, T0));
    _offset += text.Length + 1;
    return this;
  }

  // Hook events

  public ClaudeCodeSession UserPromptSubmit(string promptId, string prompt, double at, CaptureContext? context = null) =>
    Hook("UserPromptSubmit", at, new JsonObject { ["prompt_id"] = promptId, ["prompt"] = prompt }, context);

  public ClaudeCodeSession PreToolUse(string promptId, string toolUseId, double at, string tool = "Bash",
    string input = """{"command":"ls"}""") =>
    Hook("PreToolUse", at, new JsonObject
    {
      ["prompt_id"] = promptId,
      ["tool_use_id"] = toolUseId,
      ["tool_name"] = tool,
      ["tool_input"] = JsonNode.Parse(input),
    });

  public ClaudeCodeSession PostToolUse(string promptId, string toolUseId, double at, string tool = "Bash",
    string response = """{"stdout":"a.txt","stderr":""}""") =>
    Hook("PostToolUse", at, new JsonObject
    {
      ["prompt_id"] = promptId,
      ["tool_use_id"] = toolUseId,
      ["tool_name"] = tool,
      ["tool_input"] = new JsonObject { ["command"] = "ls" },
      ["tool_response"] = JsonNode.Parse(response),
    });

  public ClaudeCodeSession PostToolUseFailure(string promptId, string toolUseId, double at, string error = "Exit code 1") =>
    Hook("PostToolUseFailure", at, new JsonObject
    {
      ["prompt_id"] = promptId,
      ["tool_use_id"] = toolUseId,
      ["tool_name"] = "Bash",
      ["tool_input"] = new JsonObject { ["command"] = "ls" },
      ["error"] = error,
      ["is_interrupt"] = false,
    });

  public ClaudeCodeSession Stop(string promptId, double at) =>
    Hook("Stop", at, new JsonObject { ["prompt_id"] = promptId, ["last_assistant_message"] = "Done." });

  public ClaudeCodeSession StopFailure(string promptId, double at) =>
    Hook("StopFailure", at, new JsonObject { ["prompt_id"] = promptId, ["error"] = "overloaded" });

  public ClaudeCodeSession SessionStart(double at) => Hook("SessionStart", at, new JsonObject { ["source"] = "startup" });

  public ClaudeCodeSession Hook(string eventType, double at, JsonObject fields, CaptureContext? context = null,
    AgentEventId? eventId = null)
  {
    fields["session_id"] = Id;
    fields["hook_event_name"] = eventType;
    fields["cwd"] = "/workspace";
    _receipts.Add(new AgentEventReceipt(ReceiptId.From(Guid.CreateVersion7()), eventId ?? AgentEventId.From(Guid.CreateVersion7()),
      AgentKind.ClaudeCode, eventType, Id, At(at), At(at).AddMilliseconds(30), context ?? Context,
      new Dictionary<string, string>(), fields.ToJsonString()));
    return this;
  }

  // The same event delivered again (ADR 0010): a new receipt with the same event ID.
  public ClaudeCodeSession ResendLastEvent()
  {
    var last = _receipts[^1];
    _receipts.Add(new AgentEventReceipt(ReceiptId.From(Guid.CreateVersion7()), last.EventId, last.Agent, last.EventType,
      last.NativeSessionId, last.CapturedAt, last.ReceivedAt.AddSeconds(5), last.Context, last.Tags, last.Payload));
    return this;
  }

  public IReadOnlyList<TranscriptLine> Lines => _lines;
  public IReadOnlyList<AgentEventReceipt> Receipts => _receipts;

  public SessionEvidence Evidence() => new(AgentKind.ClaudeCode, Id, _receipts, _lines);

  public ProjectedSession Project() => new ClaudeCodeAdapter().Project(Evidence());

  public static StepId StepIdFor(StepKind kind, string key) => StepId.For(AgentKind.ClaudeCode, Id, kind, key);

  private JsonObject UserLine(string promptId, double at) => new()
  {
    ["type"] = "user",
    ["promptId"] = promptId,
    ["timestamp"] = Timestamp(at),
    ["cwd"] = "/workspace/src",
    ["gitBranch"] = "feature/cart",
  };

  private ClaudeCodeSession Assistant(string messageId, double at, JsonObject block, Usage? usage, string stopReason = "tool_use",
    string model = "claude-opus-5-5")
  {
    var u = usage ?? Usage.Default;
    var line = new JsonObject
    {
      ["type"] = "assistant",
      ["timestamp"] = Timestamp(at),
      ["requestId"] = "req_" + messageId,
      ["message"] = new JsonObject
      {
        ["id"] = messageId,
        ["model"] = model,
        ["role"] = "assistant",
        ["content"] = new JsonArray(block),
        ["stop_reason"] = stopReason,
        ["usage"] = new JsonObject
        {
          ["input_tokens"] = u.Input,
          ["cache_creation_input_tokens"] = u.CacheWrite5m + u.CacheWrite1h,
          ["cache_read_input_tokens"] = u.CacheRead,
          ["output_tokens"] = u.Output,
          ["output_tokens_details"] = new JsonObject { ["thinking_tokens"] = u.Thinking },
          ["server_tool_use"] = new JsonObject { ["web_search_requests"] = u.WebSearches, ["web_fetch_requests"] = u.WebFetches },
          ["service_tier"] = "standard",
          ["cache_creation"] = new JsonObject
          {
            ["ephemeral_1h_input_tokens"] = u.CacheWrite1h,
            ["ephemeral_5m_input_tokens"] = u.CacheWrite5m,
          },
          ["speed"] = "standard",
        },
      },
    };
    return Line(line);
  }

  private ClaudeCodeSession Line(JsonObject line)
  {
    line["uuid"] = Guid.NewGuid().ToString();
    line["parentUuid"] = _lastUuid;
    line["sessionId"] = Id;
    line["isSidechain"] = false;
    _lastUuid = line["uuid"]!.GetValue<string>();
    return RawLine(line.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
  }

  private static string Timestamp(double at) => At(at).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
}

internal sealed record Usage(long Input, long Output, long CacheWrite5m, long CacheWrite1h, long CacheRead, long Thinking,
  long WebSearches = 0, long WebFetches = 0)
{
  public static readonly Usage Default = new(3, 100, 0, 50, 4000, 10);

  public TokenUsage ToTokenUsage() => new(Input, Output, CacheWrite5m, CacheWrite1h, CacheRead, Thinking, WebSearches, WebFetches);
}
