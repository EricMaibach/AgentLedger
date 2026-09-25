using System.Text;
using System.Text.Json;

namespace AgentLedger.Cli.Envelope;

// Written by hand with Utf8JsonWriter rather than JsonSerializer: WriteRawValue embeds the payload's
// exact text (whitespace, key order, duplicate keys), which serializing an object would not preserve.
// It also needs no reflection, so it is Native AOT-safe without a source-generated context.
internal static class EnvelopeJson
{
  public static string Write(EventEnvelope envelope)
  {
    using var stream = new MemoryStream();
    using (var json = new Utf8JsonWriter(stream))
    {
      json.WriteStartObject();
      json.WriteString("eventId", envelope.EventId);
      json.WriteString("agent", envelope.Agent);
      json.WriteString("eventType", envelope.EventType);
      json.WriteString("nativeSessionId", envelope.NativeSessionId);
      json.WriteString("capturedAt", envelope.CapturedAt);
      json.WriteString("host", envelope.Host);
      json.WriteString("user", envelope.User);
      json.WriteString("projectDir", envelope.ProjectDir);
      json.WriteString("gitRepo", envelope.GitRepo);
      json.WriteString("gitBranch", envelope.GitBranch);
      json.WriteString("gitWorktree", envelope.GitWorktree);

      json.WriteStartObject("tags");
      foreach (var (key, value) in envelope.Tags)
      {
        json.WriteString(key, value);
      }

      json.WriteEndObject();

      json.WritePropertyName("payload");
      if (IsJson(envelope.Payload))
      {
        json.WriteRawValue(envelope.Payload, skipInputValidation: true);
      }
      else
      {
        json.WriteStringValue(envelope.Payload); // keep the event rather than lose it
      }

      json.WriteEndObject();
    }

    return Encoding.UTF8.GetString(stream.ToArray());
  }

  private static bool IsJson(string text)
  {
    try
    {
      using var _ = JsonDocument.Parse(text);
      return true;
    }
    catch (JsonException)
    {
      return false;
    }
  }
}
