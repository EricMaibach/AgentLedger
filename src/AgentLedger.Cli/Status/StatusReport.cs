using System.Diagnostics;
using System.Text;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Status;

internal sealed record ApiCheck(bool Reachable, string Detail);

/// <summary>What the ledger said its version is (null when it didn't answer), and whether this is the installed binary.</summary>
internal sealed record VersionCheck(string? LedgerVersion, bool Installed);

/// <summary>The `status` output: everything needed to see why events might not be arriving.</summary>
internal static class StatusReport
{
  // The CLI follows the ledger's version when it can (ADR 0024); this says whether it will.
  private static string VersionNote(CliSettings settings, VersionCheck? version) => version?.LedgerVersion switch
  {
    null => "",
    var ledger when CliVersion.Release(ledger) == CliVersion.Release(CliVersion.Current) => "same as the ledger",
    var ledger when !settings.AutoUpdate.Value => $"the ledger is {CliVersion.Release(ledger)}; auto-update is off",
    var ledger when !version.Installed => $"the ledger is {CliVersion.Release(ledger)}; not the installed binary, so it doesn't update itself",
    var ledger => $"updates itself to {CliVersion.Release(ledger)} at its next hook",
  };

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

  public static string Render(CliSettings settings, ApiCheck api, Login.ApiKeyCheck? key, SpoolSize spool, SpoolSize transcriptSpool, int trackedTranscripts,
    SpoolSize gitSpool, int watchedRepositories, string logFile, IReadOnlyList<string> recentLog,
    IReadOnlyList<string> installedScopes, VersionCheck? version = null)
  {
    var report = new StringBuilder();
    report.AppendLine("AgentLedger status");
    report.AppendLine();
    Row(report, "CLI", CliVersion.Release(CliVersion.Current), VersionNote(settings, version));
    Row(report, "API", settings.Url.Value.ToString(), settings.Url.Source);
    Row(report, "", api.Detail, "");
    Row(report, "API key", settings.ApiKey.Value is null ? "(none)" : "(set)", settings.ApiKey.Source); // never the key itself
    if (key is not null)
    {
      Row(report, "", key.Detail, "");
    }

    Row(report, "Enabled", settings.Enabled.Value ? "yes" : "no (this project opted out)", settings.Enabled.Source);
    Row(report, "Timeout", $"{settings.Timeout.Value.TotalMilliseconds:0} ms", settings.Timeout.Source);
    Row(report, "Spool", settings.SpoolDirectory.Value, settings.SpoolDirectory.Source);
    Row(report, "", $"{spool.Count} events waiting ({spool.Bytes / 1024.0:0.0} KB)", "");
    Row(report, "", $"{transcriptSpool.Count} transcript batches waiting ({transcriptSpool.Bytes / (1024.0 * 1024):0.0} MB)", "");
    Row(report, "", $"{trackedTranscripts} transcript files read", "");
    Row(report, "", $"{gitSpool.Count} git batches waiting ({gitSpool.Bytes / 1024.0:0.0} KB)", "");
    Row(report, "", $"{watchedRepositories} repositories watched for commits", "");
    Row(report, "Log", logFile, "");
    Row(report, "Hooks", installedScopes.Count == 0
      ? "claude-code: not installed (run: agentledger install claude-code)"
      : $"claude-code: {string.Join(", ", installedScopes)}", "");

    var warnings = settings.Warnings.ToList();
    if (settings.ApiKey.Value is null)
    {
      warnings.Add("No API key: the ledger refuses events without an ingest key, so they wait in the spool. Run: agentledger login");
    }
    else if (key?.Result == Login.ApiKeyCheckResult.Rejected)
    {
      warnings.Add("The ledger rejected the API key, so events wait in the spool. Create a new key and run: agentledger login");
    }
    else if (key is { Result: Login.ApiKeyCheckResult.Accepted, Scope: not "ingest" })
    {
      warnings.Add("The API key isn't an ingest key, so the ledger refuses events. Create an ingest key and run: agentledger login");
    }

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
