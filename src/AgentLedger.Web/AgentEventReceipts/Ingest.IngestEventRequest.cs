using System.Text.Json;

namespace AgentLedger.Web.AgentEventReceipts;

/// <summary>
/// The envelope the agentledger CLI sends for each hook invocation. JSON fields are camelCase.
/// </summary>
public sealed class IngestEventRequest
{
  public const string Route = "/events";

  /// <summary>Client-generated UUIDv7; the same for every resend of this event.</summary>
  public Guid EventId { get; set; }

  /// <summary>Kebab-case agent name, e.g. <c>claude-code</c>, <c>copilot-cli</c>.</summary>
  public string Agent { get; set; } = string.Empty;

  /// <summary>The agent's native hook event name, e.g. <c>PreToolUse</c>.</summary>
  public string EventType { get; set; } = string.Empty;

  public string? NativeSessionId { get; set; }
  public DateTimeOffset CapturedAt { get; set; }
  public string? Host { get; set; }
  public string? User { get; set; }
  public string? ProjectDir { get; set; }
  public string? GitRepo { get; set; }
  public string? GitBranch { get; set; }
  public string? GitWorktree { get; set; }

  /// <summary>From <c>AGENTLEDGER_TAG_*</c>; optional.</summary>
  public Dictionary<string, string>? Tags { get; set; }

  /// <summary>
  /// The hook payload as raw JSON. Bound as a JsonElement so its exact text can be stored:
  /// deserializing it into an object would lose whitespace and key order (ADR 0009).
  /// </summary>
  public JsonElement Payload { get; set; }
}
