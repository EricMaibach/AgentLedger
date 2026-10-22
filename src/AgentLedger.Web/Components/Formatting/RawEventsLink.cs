using AgentLedger.UseCases;

namespace AgentLedger.Web.Components.Formatting;

/// <summary>The raw event list's query-string parameters, using the read API's names (ADR 0016).</summary>
public sealed record RawEventsQueryString(
  string? Agent = null, string? EventType = null, string? Session = null, string? Host = null, string? Repo = null,
  string? From = null, string? To = null, bool Distinct = true, int Limit = Constants.DEFAULT_PAGE_SIZE, string? After = null);

/// <summary>Builds links to the raw event list, keeping filters in the URL and leaving defaults out (docs/ui.md).</summary>
public static class RawEventsLink
{
  public const string Path = "/raw/events";

  public static string To(RawEventsQueryString query)
  {
    var parameters = new List<string>();
    Add("agent", query.Agent);
    Add("eventType", query.EventType);
    Add("session", query.Session);
    Add("host", query.Host);
    Add("repo", query.Repo);
    Add("from", query.From);
    Add("to", query.To);
    Add("distinct", query.Distinct ? null : "false");
    Add("limit", query.Limit == Constants.DEFAULT_PAGE_SIZE ? null : query.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture));
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

  public static string ToEvent(Guid eventId) => $"{Path}/{eventId}";
}
