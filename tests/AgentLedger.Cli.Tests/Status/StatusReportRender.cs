using AgentLedger.Cli.Configuration;
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
    IReadOnlyList<string>? installedScopes = null) =>
    StatusReport.Render(
      settings ?? Settings(),
      api ?? new ApiCheck(Reachable: true, "reachable (12 ms)"),
      new SpoolSize(Count: 3, Bytes: 2048),
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
