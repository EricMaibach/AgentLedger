namespace AgentLedger.Cli.Tests.Install;

public sealed class InstallCommandRun : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly FakeHostEnvironment _env = new();
  private readonly StringWriter _stdout = new();
  private readonly StringWriter _stderr = new();
  private readonly string _binDir;
  private readonly string _repo;

  public InstallCommandRun()
  {
    _env.HomeDirectory = _dir.CreateDirectory("home");
    _repo = _env.CurrentDirectory = _dir.CreateDirectory("repo");
    _dir.Write("repo/.git/HEAD", "ref: refs/heads/main\n");      // a repository root
    _binDir = _dir.CreateDirectory("bin");
    _dir.Write("bin/agentledger", "");                              // agentledger is on PATH
    _env.Variables["PATH"] = _binDir;
  }

  public void Dispose() => _dir.Dispose();

  private Task<int> RunAsync(params string[] args) => CliApp.RunAsync(args, _env, new StringReader(""), _stdout, _stderr);

  private string UserSettings => Path.Combine(_env.HomeDirectory, ".claude", "settings.json");
  private string LocalSettings => Path.Combine(_repo, ".claude", "settings.local.json");
  private string ProjectSettings => Path.Combine(_repo, ".claude", "settings.json");

  [Fact]
  public async Task InstallsIntoUserSettingsByDefault()
  {
    var exitCode = await RunAsync("install", "claude-code");

    exitCode.ShouldBe(0);
    File.ReadAllText(UserSettings).ShouldContain("agentledger hook claude-code");
    _stdout.ToString().ShouldContain(UserSettings);
  }

  [Fact]
  public async Task UsesClaudeConfigDirForUserSettings()
  {
    var configDir = _dir.CreateDirectory("claude-config");
    _env.Variables["CLAUDE_CONFIG_DIR"] = configDir;

    await RunAsync("install", "claude-code");

    File.Exists(Path.Combine(configDir, "settings.json")).ShouldBeTrue();
  }

  [Theory]
  [InlineData("local")]
  [InlineData("project")]
  public async Task InstallsIntoTheProjectForOtherScopes(string scope)
  {
    var nested = _dir.CreateDirectory("repo/src");
    _env.CurrentDirectory = nested; // settings belong at the repository root, not the subfolder

    await RunAsync("install", "claude-code", "--scope", scope);

    File.Exists(scope == "local" ? LocalSettings : ProjectSettings).ShouldBeTrue();
  }

  [Fact]
  public async Task WarnsWhenAgentLedgerIsNotOnPath()
  {
    _env.Variables["PATH"] = _dir.CreateDirectory("empty-bin");

    var exitCode = await RunAsync("install", "claude-code");

    exitCode.ShouldBe(0);
    _stdout.ToString().ShouldContain("not found on PATH");
    _stdout.ToString().ShouldContain("--absolute-path");
  }

  [Fact]
  public async Task WritesTheFullPathWithAbsolutePath()
  {
    await RunAsync("install", "claude-code", "--absolute-path");

    File.ReadAllText(UserSettings).ShouldContain("/opt/agentledger/agentledger hook claude-code");
  }

  [Fact]
  public async Task WarnsWhenAlsoInstalledInAnotherScope()
  {
    // Installed twice, every event would be recorded twice with different event IDs (ADR 0013).
    await RunAsync("install", "claude-code", "--scope", "local");

    await RunAsync("install", "claude-code");

    _stdout.ToString().ShouldContain("also installed in local scope");
  }

  [Fact]
  public async Task UninstallsFromTheChosenScope()
  {
    await RunAsync("install", "claude-code");

    var exitCode = await RunAsync("uninstall", "claude-code");

    exitCode.ShouldBe(0);
    File.ReadAllText(UserSettings).ShouldNotContain("agentledger");
    _stdout.ToString().ShouldContain("Removed 27");
  }

  [Fact]
  public async Task RejectsAnAgentItCannotInstallForYet()
  {
    var exitCode = await RunAsync("install", "codex");

    exitCode.ShouldBe(1);
    _stderr.ToString().ShouldContain("claude-code");
  }

  [Fact]
  public async Task ReportsASettingsFileItCannotEdit()
  {
    _dir.Write("home/.claude/settings.json", "{ not json");

    var exitCode = await RunAsync("install", "claude-code");

    exitCode.ShouldBe(1);
    _stderr.ToString().ShouldContain(UserSettings);
  }
}
