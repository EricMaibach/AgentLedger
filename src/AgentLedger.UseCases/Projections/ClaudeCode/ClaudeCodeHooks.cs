using System.Text.Json;
using AgentLedger.Core.AgentEventReceiptAggregate;

namespace AgentLedger.UseCases.Projections.ClaudeCode;

// One hook event as the adapter uses it: the first receipt of each event ID (ADR 0010) and the payload fields that
// matter for steps. AgentId is set on a subagent's events.
internal sealed record HookEvent(AgentEventReceipt Receipt, string? PromptId, string? AgentId, string? ToolUseId,
  string? ToolName, string? ToolInput, string? ToolResponse, string? Error, string? Prompt)
{
  public AgentEventId EventId => Receipt.EventId;
  public string Type => Receipt.EventType;
  public DateTimeOffset At => Receipt.CapturedAt;

  public static IReadOnlyList<HookEvent> Read(IEnumerable<AgentEventReceipt> receipts)
  {
    var events = new List<HookEvent>();
    foreach (var receipt in receipts.GroupBy(r => r.EventId).Select(g => g.OrderBy(r => r.ReceivedAt).First())
               .OrderBy(r => r.CapturedAt))
    {
      try
      {
        using var json = JsonDocument.Parse(receipt.Payload);
        var p = json.RootElement;
        events.Add(new HookEvent(receipt, ClaudeCodeTranscript.String(p, "prompt_id"), ClaudeCodeTranscript.String(p, "agent_id"),
          ClaudeCodeTranscript.String(p, "tool_use_id"),
          ClaudeCodeTranscript.String(p, "tool_name"), Raw(p, "tool_input"), Raw(p, "tool_response"),
          ClaudeCodeTranscript.String(p, "error"), ClaudeCodeTranscript.String(p, "prompt")));
      }
      catch (JsonException)
      {
        // Payloads are stored as sent (ADR 0009); one that isn't JSON has nothing to project.
      }
    }
    return events;
  }

  private static string? Raw(JsonElement payload, string name) =>
    payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var value) ? value.GetRawText() : null;
}
