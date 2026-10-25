using System.Text;
using System.Text.Json;

namespace AgentLedger.Web.Components.Formatting;

/// <summary>Summaries of a transcript line for the list (docs/ui.md, TranscriptLines). Raw: the line's own field, as written.</summary>
public static class TranscriptLineDisplay
{
  /// <summary>Shown when a line isn't JSON.</summary>
  public const string NotJson = "not JSON";

  /// <summary>Characters of a line shown before the ellipsis.</summary>
  public const int PreviewLength = 160;

  /// <summary>The line's own top-level string <c>type</c> field; null without one; <see cref="NotJson"/> if it isn't JSON.</summary>
  public static string? TypeOf(string line)
  {
    try
    {
      using var json = JsonDocument.Parse(line);
      return json.RootElement.ValueKind == JsonValueKind.Object
             && json.RootElement.TryGetProperty("type", out var type)
             && type.ValueKind == JsonValueKind.String
        ? type.GetString()
        : null;
    }
    catch (JsonException)
    {
      return NotJson;
    }
  }

  /// <summary>One line of text, cut with an ellipsis after <see cref="PreviewLength"/> characters.</summary>
  public static string Preview(string line)
  {
    var flat = new StringBuilder(Math.Min(line.Length, PreviewLength + 1));
    var lastWasSpace = false;
    foreach (var c in line)
    {
      if (flat.Length == PreviewLength)
      {
        return flat.Append('…').ToString();
      }

      var isSpace = char.IsWhiteSpace(c);
      if (isSpace && lastWasSpace)
      {
        continue; // collapse runs such as "\r\n" into one space
      }

      flat.Append(isSpace ? ' ' : c);
      lastWasSpace = isSpace;
    }

    return flat.ToString();
  }
}
