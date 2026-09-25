using System.Text.Json.Nodes;
using AgentLedger.Cli.Install;

namespace AgentLedger.Cli.Tests.Install;

public sealed class ClaudeCodeHookSettingsTests : IDisposable
{
  private const string Command = "agentledger hook claude-code";

  private readonly TempDirectory _dir = new();
  private readonly string _file;

  public ClaudeCodeHookSettingsTests() => _file = Path.Combine(_dir.Path, ".claude", "settings.json");

  public void Dispose() => _dir.Dispose();

  private JsonObject ReadFile() => JsonNode.Parse(File.ReadAllText(_file))!.AsObject();

  // Every hook object in the file, with the event it's registered under.
  private List<(string Event, JsonObject Hook)> AllHooks() =>
    ReadFile()["hooks"]?.AsObject().SelectMany(evt => evt.Value!.AsArray()
      .SelectMany(group => group!["hooks"]!.AsArray().Select(hook => (evt.Key, hook!.AsObject())))).ToList() ?? [];

  [Fact]
  public void AddsAnAsyncEntryForEveryEventExceptASynchronousSessionEnd()
  {
    ClaudeCodeHookSettings.Install(_file, Command);

    var hooks = AllHooks();
    hooks.Select(h => h.Event).ShouldBe(ClaudeCodeHookSettings.Events, ignoreOrder: true);
    hooks.ShouldAllBe(h => h.Hook["command"]!.GetValue<string>() == Command);
    hooks.Where(h => h.Event != "SessionEnd").ShouldAllBe(h => h.Hook["async"]!.GetValue<bool>());
    var sessionEnd = hooks.Single(h => h.Event == "SessionEnd").Hook;
    sessionEnd["async"].ShouldBeNull(); // async hooks can be cut off as the session ends
    sessionEnd["timeout"]!.GetValue<int>().ShouldBe(2);
  }

  [Fact]
  public void CoversTheTwentySevenClaudeCodeHookEvents()
  {
    ClaudeCodeHookSettings.Events.Count.ShouldBe(27);
    ClaudeCodeHookSettings.Events.ShouldContain("PreToolUse");
    ClaudeCodeHookSettings.Events.ShouldContain("SessionEnd");
  }

  [Fact]
  public void KeepsOtherSettingsAndOtherHooks()
  {
    _dir.Write(".claude/settings.json", """
      { "theme": "dark",
        "hooks": { "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "./audit.sh" } ] } ] } }
      """);

    ClaudeCodeHookSettings.Install(_file, Command);

    ReadFile()["theme"]!.GetValue<string>().ShouldBe("dark");
    AllHooks().ShouldContain(h => h.Event == "PreToolUse" && h.Hook["command"]!.GetValue<string>() == "./audit.sh");
  }

  [Fact]
  public void IsIdempotent()
  {
    ClaudeCodeHookSettings.Install(_file, Command);
    ClaudeCodeHookSettings.Install(_file, Command);

    AllHooks().Count.ShouldBe(27);
  }

  [Fact]
  public void ReplacesEarlierEntriesWhenTheCommandChanges()
  {
    ClaudeCodeHookSettings.Install(_file, Command);

    ClaudeCodeHookSettings.Install(_file, "/opt/agentledger/agentledger hook claude-code");

    AllHooks().ShouldAllBe(h => h.Hook["command"]!.GetValue<string>() == "/opt/agentledger/agentledger hook claude-code");
    AllHooks().Count.ShouldBe(27);
  }

  [Fact]
  public void UninstallRemovesOnlyAgentLedgerEntries()
  {
    _dir.Write(".claude/settings.json", """
      { "hooks": { "Stop": [ { "matcher": "*", "hooks": [ { "type": "command", "command": "./notify.sh" } ] } ] } }
      """);
    ClaudeCodeHookSettings.Install(_file, Command);

    var removed = ClaudeCodeHookSettings.Uninstall(_file);

    removed.ShouldBe(27);
    AllHooks().ShouldHaveSingleItem().Hook["command"]!.GetValue<string>().ShouldBe("./notify.sh");
  }

  [Fact]
  public void UninstallLeavesNoEmptyHooksBehind()
  {
    ClaudeCodeHookSettings.Install(_file, Command);

    ClaudeCodeHookSettings.Uninstall(_file);

    ReadFile().ContainsKey("hooks").ShouldBeFalse();
  }

  [Fact]
  public void ReportsWhetherItIsInstalled()
  {
    ClaudeCodeHookSettings.IsInstalled(_file).ShouldBeFalse(); // no file yet

    ClaudeCodeHookSettings.Install(_file, Command);

    ClaudeCodeHookSettings.IsInstalled(_file).ShouldBeTrue();
  }

  [Fact]
  public void RefusesToEditAFileThatIsNotValidJson()
  {
    _dir.Write(".claude/settings.json", "{ not json");

    Should.Throw<InvalidDataException>(() => ClaudeCodeHookSettings.Install(_file, Command));
    File.ReadAllText(_file).ShouldBe("{ not json"); // untouched
  }

  [Theory]
  [InlineData("agentledger hook claude-code", true)]
  [InlineData("/home/eric/.local/bin/agentledger hook claude-code", true)]
  [InlineData("\"C:\\Program Files\\AgentLedger\\agentledger.exe\" hook claude-code", true)]
  [InlineData("agentledger hook codex", false)]                       // another agent's entry
  [InlineData("\"$CLAUDE_PROJECT_DIR\"/tools/probe/capture.sh claude-code PreToolUse", false)]
  [InlineData("not-agentledger hook claude-code", false)]
  public void RecognizesItsOwnEntries(string command, bool expected)
  {
    ClaudeCodeHookSettings.IsOurs(command).ShouldBe(expected);
  }
}
