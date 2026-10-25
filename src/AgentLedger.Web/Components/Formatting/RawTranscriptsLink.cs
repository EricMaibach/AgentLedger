using System.Globalization;
using AgentLedger.UseCases;

namespace AgentLedger.Web.Components.Formatting;

/// <summary>The raw transcripts list's query-string parameters, using the read API's names (docs/ui.md).</summary>
public sealed record RawTranscriptsQueryString(
  string? Agent = null, string? LineType = null, string? Session = null, string? File = null, string? From = null, string? To = null,
  int Limit = Constants.DEFAULT_PAGE_SIZE, string? After = null);

/// <summary>Links to the raw transcripts pages, keeping filters in the URL and leaving defaults out (docs/ui.md).</summary>
public static class RawTranscriptsLink
{
  /// <summary>The list's path.</summary>
  public const string Path = "/raw/transcripts";

  /// <summary>The list with these parameters.</summary>
  public static string To(RawTranscriptsQueryString query)
  {
    var parameters = new List<string>();
    Add("agent", query.Agent);
    Add("lineType", query.LineType);
    Add("session", query.Session);
    Add("file", query.File);
    Add("from", query.From);
    Add("to", query.To);
    Add("limit", query.Limit == Constants.DEFAULT_PAGE_SIZE ? null : query.Limit.ToString(CultureInfo.InvariantCulture));
    Add("after", query.After);

    return parameters.Count == 0 ? Path : $"{Path}?{string.Join('&', parameters)}";

    void Add(string name, string? value)
    {
      if (!string.IsNullOrEmpty(value))
      {
        parameters.Add($"{name}={Uri.EscapeDataString(value)}");
      }
    }
  }

  /// <summary>A line's page.</summary>
  public static string ToLine(Guid lineId) => $"{Path}/{lineId}";
}
