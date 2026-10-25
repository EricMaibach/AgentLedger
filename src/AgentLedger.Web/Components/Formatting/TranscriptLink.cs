namespace AgentLedger.Web.Components.Formatting;

/// <summary>Links to a session's transcript page, leaving defaults out (docs/ui.md, Transcript page).</summary>
public static class TranscriptLink
{
  /// <summary>The file shown when none is chosen.</summary>
  public const string MainFile = "main";

  /// <summary>The page for a session's transcript: a file (main by default), a view and a page.</summary>
  public static string To(string session, string? file = null, bool exact = false, string? after = null)
  {
    var parameters = new List<string>();
    if (file is { Length: > 0 } && file != MainFile)
    {
      parameters.Add($"file={Uri.EscapeDataString(file)}");
    }

    if (exact)
    {
      parameters.Add("view=exact");
    }

    if (after is { Length: > 0 })
    {
      parameters.Add($"after={Uri.EscapeDataString(after)}");
    }

    var path = $"/raw/sessions/{Uri.EscapeDataString(session)}/transcript";
    return parameters.Count == 0 ? path : $"{path}?{string.Join('&', parameters)}";
  }
}
