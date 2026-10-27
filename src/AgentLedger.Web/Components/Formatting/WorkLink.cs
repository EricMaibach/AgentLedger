namespace AgentLedger.Web.Components.Formatting;

/// <summary>Links in the work section (ADR 0023): the selected unit, turn and step live in the query string.</summary>
public static class WorkLink
{
  public const string Path = "/work";

  // The session lens is the only one so far, and the default, so it's left out of the URL (docs/ui.md).
  public static string To(Guid unit, Guid? turn = null, Guid? step = null) =>
    Path + $"?unit={unit}" + (turn is { } t ? $"&turn={t}" : "") + (step is { } s ? $"&step={s}" : "");

  public static string RawEvent(Guid eventId) => $"/raw/events/{eventId}";

  public static string RawLine(Guid lineId) => $"/raw/transcripts/{lineId}";
}
