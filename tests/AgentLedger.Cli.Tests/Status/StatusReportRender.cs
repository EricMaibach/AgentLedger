using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Login;
using AgentLedger.Cli.Spooling;
using AgentLedger.Cli.Status;

namespace AgentLedger.Cli.Tests.Status;

public sealed class StatusReportRender
{
  private static CliSettings Settings(string? apiKey = null, IReadOnlyList<string>? warnings = null) => new(
    Url: new Setting<Uri>(new Uri("http://api.test:5000"), "project (/work/repo/.agentledger.json)"),
    ApiKey: new Setting<string?>(apiKey, apiKey is null ? "default" : "environment (AGENTLEDGER_API_KEY)"),
    Timeout: new Setting<TimeSpan>(TimeSpan.FromSeconds(1), "default"),
    SpoolDirectory: new Setting<string>("/home/eric/.local/share/agentledger/spool", "default"),
    Enabled: new Setting<bool>(true, "default"),
    Warnings: warnings ?? []);

  private static string Render(CliSettings? settings = null, ApiCheck? api = null, IReadOnlyList<string>? recentLog = null,
    IReadOnlyList<string>? installedScopes = null, ApiKeyCheck? key = null, SpoolSize? transcriptSpool = null, int trackedTranscripts = 0,
    SpoolSize? gitSpool = null, int watchedRepositories = 0) =>
    StatusReport.Render(
      settings ?? Settings(),
      api ?? new ApiCheck(Reachable: true, "reachable (12 ms)"),
      key,
      new SpoolSize(Count: 3, Bytes: 2048),
      transcriptSpool ?? new SpoolSize(Count: 0, Bytes: 0),
      trackedTranscripts,
      gitSpool ?? new SpoolSize(Count: 0, Bytes: 0),
      watchedRepositories,
      "/home/eric/.local/share/agentledger/agentledger.log",
      recentLog ?? [],
      installedScopes ?? ["user"]);

  [Fact]
  public void ShowsEachSettingWithWhereItCameFrom()
  {
    var report = Render();

    report.ShouldContain("http://api.test:5000/");
    report.ShouldContain("project (/work/repo/.agentledger.json)");
    report.ShouldContain("1000 ms");
    report.ShouldContain("/home/eric/.local/share/agentledger/spool");
  }

  [Fact]
  public void NeverShowsTheApiKeyItself()
  {
    var report = Render(Settings(apiKey: "super-secret-key"));

    report.ShouldNotContain("super-secret-key");
    report.ShouldContain("environment (AGENTLEDGER_API_KEY)");
  }

  [Fact]
  public void ShowsWhatTheLedgerSaysAboutTheKey()
  {
    var key = new ApiKeyCheck(ApiKeyCheckResult.Accepted, "accepted: 'laptop' (ingest, owner eric)", "laptop", "ingest", "eric");

    Render(Settings(apiKey: "agl_ingest_x_y"), key: key).ShouldContain("accepted: 'laptop' (ingest, owner eric)");
  }

  [Fact]
  public void ShowsTranscriptBatchesWaitingAndFilesBeingRead()
  {
    var report = Render(transcriptSpool: new SpoolSize(Count: 4, Bytes: 3 * 1024 * 1024), trackedTranscripts: 7);

    report.ShouldContain("4 transcript batches waiting (3.0 MB)");
    report.ShouldContain("7 transcript files read");
  }

  [Fact]
  public void ShowsGitBatchesWaitingAndRepositoriesWatched()
  {
    var report = Render(gitSpool: new SpoolSize(Count: 2, Bytes: 2048), watchedRepositories: 3);

    report.ShouldContain("2 git batches waiting (2.0 KB)");
    report.ShouldContain("3 repositories watched for commits");
  }

  [Fact]
  public void WarnsWithoutAKey()
  {
    // The ledger refuses events without an ingest key (ADR 0019); they wait in the spool.
    Render(Settings(apiKey: null)).ShouldContain("agentledger login");
  }

  [Fact]
  public void WarnsAboutARejectedKey()
  {
    var key = new ApiKeyCheck(ApiKeyCheckResult.Rejected, "rejected: unknown, revoked or expired", null, null, null);

    var report = Render(Settings(apiKey: "agl_ingest_x_y"), key: key);

    report.ShouldContain("rejected: unknown, revoked or expired");
    report.ShouldContain("agentledger login");
  }

  [Fact]
  public void ShowsWhetherTheApiIsReachable()
  {
    Render(api: new ApiCheck(false, "not reachable: Connection refused")).ShouldContain("not reachable: Connection refused");
  }

  [Fact]
  public void ShowsWhereTheHooksAreInstalled()
  {
    Render(installedScopes: ["user", "local"]).ShouldContain("claude-code: user, local");
  }

  [Fact]
  public void WarnsWhenTheHooksAreInstalledInMoreThanOneScope()
  {
    Render(installedScopes: ["user", "local"]).ShouldContain("recorded twice");
  }

  [Fact]
  public void SaysWhenTheHooksAreNotInstalled()
  {
    Render(installedScopes: []).ShouldContain("not installed");
  }

  [Fact]
  public void ShowsWaitingEventsTheLogWarningsAndRecentLogLines()
  {
    var report = Render(Settings(warnings: ["Ignored invalid AGENTLEDGER_TIMEOUT_MS: 'soon'."]), recentLog: ["2026-09-25 ERROR something broke"]);

    report.ShouldContain("3 events");
    report.ShouldContain("/home/eric/.local/share/agentledger/agentledger.log");
    report.ShouldContain("Ignored invalid AGENTLEDGER_TIMEOUT_MS");
    report.ShouldContain("something broke");
  }
}

// `status` compares the CLI's version with the ledger's (ADR 0024).
public sealed class StatusReportRenderVersions
{
  private static CliSettings Settings(bool autoUpdate = true) => new(
    Url: new Setting<Uri>(new Uri("http://api.test:5000"), "user"),
    ApiKey: new Setting<string?>("agl_ingest_x", "user"),
    Timeout: new Setting<TimeSpan>(TimeSpan.FromSeconds(1), "default"),
    SpoolDirectory: new Setting<string>("/spool", "default"),
    Enabled: new Setting<bool>(true, "default"),
    Warnings: [])
  { AutoUpdate = new Setting<bool>(autoUpdate, "default") };

  private static string Render(string? ledgerVersion, bool autoUpdate = true, bool installed = true) =>
    StatusReport.Render(Settings(autoUpdate), new ApiCheck(true, "reachable (5 ms)"), null, new SpoolSize(0, 0), new SpoolSize(0, 0), 0,
      new SpoolSize(0, 0), 0, "/log", [], ["user"], new VersionCheck(ledgerVersion, installed));

  [Fact]
  public void SaysWhenTheVersionsMatch()
  {
    Render(CliVersion.Release(CliVersion.Current) + "+build").ShouldContain("same as the ledger");
  }

  [Fact]
  public void SaysAnUpdateIsComing()
  {
    Render("99.0.0").ShouldContain("updates itself to 99.0.0");
  }

  [Fact]
  public void SaysWhyItWillNotUpdate()
  {
    Render("99.0.0", autoUpdate: false).ShouldContain("auto-update is off");
    Render("99.0.0", installed: false).ShouldContain("not the installed binary");
  }

  [Fact]
  public void ShowsTheCliVersionWhenTheLedgersIsUnknown()
  {
    Render(null).ShouldContain(CliVersion.Release(CliVersion.Current));
  }
}
