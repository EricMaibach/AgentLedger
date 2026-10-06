using System.Text.Json.Nodes;

namespace AgentLedger.FunctionalTests.ApiEndpoints;

// The envelope as the CLI sends it to POST /events (ADR 0011). The payload is spliced in as raw text,
// so its exact formatting is what goes over the wire.
internal static class TestEnvelope
{
  public const string DefaultPayload = """{"session_id":"3829bce8","hook_event_name":"PreToolUse"}""";

  public static string Json(
    Guid? eventId = null,
    string agent = "claude-code",
    string eventType = "PreToolUse",
    string? host = "devbox",
    bool includeTags = true,
    bool includePayload = true,
    string payloadJson = DefaultPayload)
  {
    var envelope = new JsonObject
    {
      ["eventId"] = eventId ?? Guid.CreateVersion7(),
      ["agent"] = agent,
      ["eventType"] = eventType,
      ["nativeSessionId"] = "3829bce8-0000-0000-0000-000000000000",
      ["capturedAt"] = new DateTimeOffset(2026, 9, 24, 9, 0, 0, TimeSpan.Zero),
      ["host"] = host,
      ["user"] = "eric",
      ["projectDir"] = "/workspace",
      ["gitRepo"] = "github.com/EricMaibach/AgentLedger",
      ["gitBranch"] = "main",
    };
    if (includeTags)
    {
      envelope["tags"] = new JsonObject { ["story"] = "123" };
    }

    var json = envelope.ToJsonString();
    return includePayload ? json[..^1] + ",\"payload\":" + payloadJson + "}" : json;
  }
}
