using System.Text;
using System.Text.Json;

namespace AgentLedger.Web.Components.Formatting;

/// <summary>Formats payloads for reading (docs/ui.md, JsonViewer).</summary>
public static class JsonDisplay
{
  /// <summary>
  /// Indents JSON, keeping key order and duplicate keys exactly as stored (it re-writes tokens; nothing is
  /// parsed into objects). Text that isn't valid JSON is returned unchanged.
  /// </summary>
  public static string Indent(string json)
  {
    try
    {
      using var document = JsonDocument.Parse(json);
      using var stream = new MemoryStream();
      using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
      {
        document.WriteTo(writer);
      }

      return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
    }
    catch (JsonException)
    {
      return json;
    }
  }
}
