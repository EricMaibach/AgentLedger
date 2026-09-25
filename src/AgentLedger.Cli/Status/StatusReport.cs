using System.Diagnostics;
using System.Text;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Status;

internal sealed record ApiCheck(bool Reachable, string Detail);

/// <summary>The `status` output: everything needed to see why events might not be arriving.</summary>
internal static class StatusReport
{
  public static async Task<ApiCheck> CheckApiAsync(HttpClient http, Uri apiUrl)
  {
    var started = Stopwatch.GetTimestamp();
    try
    {
      using var response = await http.GetAsync(new Uri(apiUrl.ToString().TrimEnd('/') + "/status"));
      var elapsed = (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
      return response.IsSuccessStatusCode
        ? new ApiCheck(true, $"reachable ({elapsed} ms)")
        : new ApiCheck(false, $"not reachable: {(int)response.StatusCode} {response.ReasonPhrase}");
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
      return new ApiCheck(false, $"not reachable: {ex.Message}");
    }
  }

  public static string Render(CliSettings settings, ApiCheck api, SpoolSize spool, string logFile, IReadOnlyList<string> recentLog,
    IReadOnlyList<string> installedScopes)
  {
    var report = new StringBuilder();
    report.AppendLine("AgentLedger status");
    report.AppendLine();
    Row(report, "API", settings.Url.Value.ToString(), settings.Url.Source);
    Row(report, "", api.Detail, "");
    Row(report, "API key", settings.ApiKey.Value is null ? "(none)" : "(set)", settings.ApiKey.Source); // never the key itself
    Row(report, "Enabled", settings.Enabled.Value ? "yes" : "no (this project opted out)", settings.Enabled.Source);
    Row(report, "Timeout", $"{settings.Timeout.Value.TotalMilliseconds:0} ms", settings.Timeout.Source);
    Row(report, "Spool", settings.SpoolDirectory.Value, settings.SpoolDirectory.Source);
    Row(report, "", $"{spool.Count} events waiting ({spool.Bytes / 1024.0:0.0} KB)", "");
    Row(report, "Log", logFile, "");
    Row(report, "Hooks", installedScopes.Count == 0
      ? "claude-code: not installed (run: agentledger install claude-code)"
      : $"claude-code: {string.Join(", ", installedScopes)}", "");

    var warnings = settings.Warnings.ToList();
    if (installedScopes.Count > 1)
    {
      warnings.Add($"Hooks are installed in {installedScopes.Count} scopes, so every event is recorded twice. Uninstall all but one.");
    }

    if (warnings.Count > 0)
    {
      report.AppendLine().AppendLine("Warnings");
      foreach (var warning in warnings)
      {
        report.AppendLine($"  - {warning}");
      }
    }

    if (recentLog.Count > 0)
    {
      report.AppendLine().AppendLine("Recent log");
      foreach (var line in recentLog)
      {
        report.AppendLine($"  {line}");
      }
    }

    return report.ToString();
  }

  private static void Row(StringBuilder report, string label, string value, string source) =>
    report.AppendLine($"  {label,-9} {value,-45} {source}".TrimEnd());
}
