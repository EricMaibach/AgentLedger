namespace AgentLedger.Cli.Tests;

public sealed class CliAppRun : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly FakeHostEnvironment _env = new();
  private readonly StringWriter _stdout = new();
  private readonly StringWriter _stderr = new();

  public CliAppRun()
  {
    _env.CurrentDirectory = _dir.CreateDirectory("project");
    _env.LocalDataDirectory = _dir.CreateDirectory("data");
    _env.UserConfigDirectory = _dir.CreateDirectory("config");
    // Port 9 (discard) refuses connections at once, so the send fails fast: the "API is down" case.
    _env.Variables["AGENTLEDGER_URL"] = "http://127.0.0.1:9";
  }

  public void Dispose() => _dir.Dispose();

  private Task<int> RunAsync(string[] args, string stdin = """{"session_id":"s1","hook_event_name":"Stop"}""") =>
    CliApp.RunAsync(args, _env, new StringReader(stdin), _stdout, _stderr);

  private string SpoolDirectory => Path.Combine(_env.LocalDataDirectory, "agentledger", "spool");

  [Fact]
  public async Task HookIsSilentAndExitsZeroEvenWhenTheApiIsDown()
  {
    // Agents treat hook output as instructions and exit code 2 as "block this action" (ADR 0012).
    var exitCode = await RunAsync(["hook", "claude-code"]);

    exitCode.ShouldBe(0);
    _stdout.ToString().ShouldBeEmpty();
    _stderr.ToString().ShouldBeEmpty();
    Directory.GetFiles(SpoolDirectory, "*.json").ShouldHaveSingleItem(); // kept for a later run
  }

  [Theory]
  [InlineData("hook")]                          // agent missing
  [InlineData("hook", "claude-code", "--nope")] // unknown option
  public async Task HookIsSilentAndExitsZeroEvenWhenCalledWrongly(params string[] args)
  {
    var exitCode = await RunAsync(args);

    exitCode.ShouldBe(0);
    _stdout.ToString().ShouldBeEmpty();
    _stderr.ToString().ShouldBeEmpty();
  }

  [Fact]
  public async Task HookRecordsNothingInAProjectThatOptedOut()
  {
    File.WriteAllText(Path.Combine(_env.CurrentDirectory, ".agentledger.json"), """{ "enabled": false }""");

    var exitCode = await RunAsync(["hook", "claude-code"]);

    exitCode.ShouldBe(0);
    _stdout.ToString().ShouldBeEmpty();
    Directory.Exists(SpoolDirectory).ShouldBeFalse(); // not even spooled
  }

  [Fact]
  public async Task HookLogsWhatHappened()
  {
    await RunAsync(["hook", "claude-code"]);

    File.ReadAllText(Path.Combine(_env.LocalDataDirectory, "agentledger", "agentledger.log")).ShouldContain("stays in the spool");
  }

  [Fact]
  public async Task StatusReportsAnUnreachableApiAndExitsOne()
  {
    var exitCode = await RunAsync(["status"]);

    exitCode.ShouldBe(1);
    _stdout.ToString().ShouldContain("not reachable");
    _stdout.ToString().ShouldContain("environment (AGENTLEDGER_URL)");
  }

  [Fact]
  public async Task FlushSaysSoWhenNothingIsWaiting()
  {
    var exitCode = await RunAsync(["flush"]);

    exitCode.ShouldBe(0);
    _stdout.ToString().ShouldContain("Nothing waiting");
  }

  [Fact]
  public async Task FlushReportsEventsItCouldNotSendAndExitsOne()
  {
    await RunAsync(["hook", "claude-code"]); // API is down, so this leaves one event in the spool

    var exitCode = await RunAsync(["flush"]);

    exitCode.ShouldBe(1);
    _stdout.ToString().ShouldContain("1 still waiting");
  }

  [Fact]
  public async Task HelpListsTheHookCommand()
  {
    await RunAsync(["--help"]);

    _stdout.ToString().ShouldContain("hook");
  }
}
