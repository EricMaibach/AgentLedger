using System.Globalization;
using System.Text.Json;
using AgentLedger.Core.Projections;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UseCases.Projections.ClaudeCode;

// What the adapter reads from a Claude Code transcript (shapes as of 2026-10-26, ADR 0020): prompts, the blocks of
// assistant messages, and tool results. Everything else is skipped for now (subagent files, attachments, system lines:
// build step 3). Order is the order of the main file.
internal sealed record PromptEntry(int Order, TranscriptLine Line, string PromptId, DateTimeOffset At, string Text,
  string? Source, string? Directory, string? GitBranch);

internal sealed record AssistantBlock(int Order, TranscriptLine Line, string? PromptId, DateTimeOffset At, string MessageId,
  string Model, string? StopReason, TokenUsage Usage, int Index, string BlockType, string? Text, string? ToolUseId,
  string? ToolName, string? ToolInput);

internal sealed record ToolResultEntry(int Order, TranscriptLine Line, string ToolUseId, DateTimeOffset At, string Output,
  bool IsError);

internal sealed class ClaudeCodeTranscript
{
  public List<PromptEntry> Prompts { get; } = [];
  public List<AssistantBlock> Blocks { get; } = [];
  public List<ToolResultEntry> Results { get; } = [];
  public List<DateTimeOffset> Times { get; } = [];

  // Claude Code's AI-generated session title: the latest one written.
  public string? AiTitle { get; private set; }

  public static ClaudeCodeTranscript Read(IEnumerable<TranscriptLine> lines)
  {
    var transcript = new ClaudeCodeTranscript();
    string? currentPrompt = null;
    var order = 0;
    foreach (var line in lines.Where(l => l.File == TranscriptFileName.Main).OrderBy(l => l.ByteOffset))
    {
      order++;
      try
      {
        using var json = JsonDocument.Parse(line.Text);
        var root = json.RootElement;
        // Title lines have no timestamp.
        if (String(root, "type") == "ai-title" && String(root, "aiTitle") is { } title)
          transcript.AiTitle = title;
        if (root.ValueKind != JsonValueKind.Object || !TryTime(root, out var at))
          continue;
        transcript.Times.Add(at);
        switch (String(root, "type"))
        {
          case "user":
            currentPrompt = String(root, "promptId") ?? currentPrompt;
            transcript.ReadUser(order, line, root, at);
            break;
          case "assistant":
            transcript.ReadAssistant(order, line, root, at, currentPrompt);
            break;
        }
      }
      catch (JsonException)
      {
        // Not JSON: kept raw in the ledger (ADR 0020), nothing to project.
      }
    }
    return transcript;
  }

  private void ReadUser(int order, TranscriptLine line, JsonElement root, DateTimeOffset at)
  {
    if (!root.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content))
      return;

    if (content.ValueKind == JsonValueKind.Array && content.EnumerateArray().Any(b => String(b, "type") == "tool_result"))
    {
      foreach (var block in content.EnumerateArray().Where(b => String(b, "type") == "tool_result"))
      {
        if (String(block, "tool_use_id") is { } toolUseId)
          Results.Add(new ToolResultEntry(order, line, toolUseId, at, ResultText(block),
            block.TryGetProperty("is_error", out var e) && e.ValueKind == JsonValueKind.True));
      }
      return;
    }

    // Meta lines (instructions, skill text) and compaction summaries are added context, not prompts.
    if (Bool(root, "isMeta") || Bool(root, "isCompactSummary") || String(root, "promptId") is not { } promptId)
      return;
    var text = content.ValueKind switch
    {
      JsonValueKind.String => content.GetString(),
      JsonValueKind.Array => string.Join("\n", content.EnumerateArray().Select(b => String(b, "text")).OfType<string>()),
      _ => null,
    };
    if (!string.IsNullOrWhiteSpace(text))
      Prompts.Add(new PromptEntry(order, line, promptId, at, text, String(root, "promptSource"), String(root, "cwd"),
        String(root, "gitBranch")));
  }

  private void ReadAssistant(int order, TranscriptLine line, JsonElement root, DateTimeOffset at, string? promptId)
  {
    if (!root.TryGetProperty("message", out var message) || String(message, "id") is not { } messageId
        || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
      return;
    var model = String(message, "model") ?? "unknown";
    var usage = Usage(message);
    var stopReason = String(message, "stop_reason");
    var index = 0;
    foreach (var block in content.EnumerateArray())
    {
      var type = String(block, "type") ?? "";
      Blocks.Add(new AssistantBlock(order, line, promptId, at, messageId, model, stopReason, usage, index++, type,
        type == "thinking" ? String(block, "thinking") : String(block, "text"),
        String(block, "id"), String(block, "name"),
        block.TryGetProperty("input", out var input) ? input.GetRawText() : null));
    }
  }

  private static TokenUsage Usage(JsonElement message)
  {
    if (!message.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object)
      return new TokenUsage(0, 0, 0, 0, 0, 0, 0, 0);
    long write5m, write1h;
    if (u.TryGetProperty("cache_creation", out var creation) && creation.ValueKind == JsonValueKind.Object)
    {
      write5m = Long(creation, "ephemeral_5m_input_tokens");
      write1h = Long(creation, "ephemeral_1h_input_tokens");
    }
    else
    {
      // Without the breakdown, cache writes are counted at the default (5-minute) duration.
      write5m = Long(u, "cache_creation_input_tokens");
      write1h = 0;
    }
    var thinking = u.TryGetProperty("output_tokens_details", out var details) ? Long(details, "thinking_tokens") : 0;
    var server = u.TryGetProperty("server_tool_use", out var s) ? s : default;
    return new TokenUsage(Long(u, "input_tokens"), Long(u, "output_tokens"), write5m, write1h,
      Long(u, "cache_read_input_tokens"), thinking, Long(server, "web_search_requests"), Long(server, "web_fetch_requests"));
  }

  // A tool result as the model saw it: its text, or the text of its text blocks.
  private static string ResultText(JsonElement block)
  {
    if (!block.TryGetProperty("content", out var content))
      return "";
    return content.ValueKind switch
    {
      JsonValueKind.String => content.GetString() ?? "",
      JsonValueKind.Array => string.Join("\n", content.EnumerateArray().Select(b => String(b, "text")).OfType<string>()),
      _ => content.GetRawText(),
    };
  }

  private static bool TryTime(JsonElement root, out DateTimeOffset at)
  {
    at = default;
    return String(root, "timestamp") is { } text
           && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out at);
  }

  internal static string? String(JsonElement element, string name) =>
    element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
      ? value.GetString()
      : null;

  private static bool Bool(JsonElement element, string name) =>
    element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

  private static long Long(JsonElement element, string name) =>
    element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt64(out var n) && n > 0
      ? n
      : 0;
}
