using System.CommandLine;
using AgentLedger.Cli.Envelope;

namespace AgentLedger.Cli.Install;

internal enum InstallScope
{
  User,
  Local,
  Project,
}

/// <summary>`agentledger install|uninstall &lt;agent&gt; [--scope user|local|project] [--absolute-path]` (ADR 0013).</summary>
internal static class InstallCommands
{
  private const string SupportedAgent = "claude-code";
  private const string BinaryName = "agentledger";

  public static IEnumerable<Command> Create(IHostEnvironment environment, TextWriter stdout, TextWriter stderr)
  {
    yield return CreateInstall(environment, stdout, stderr);
    yield return CreateUninstall(environment, stdout, stderr);
  }

  /// <summary>The Claude Code settings file for a scope. Project scopes use the repository root.</summary>
  public static string SettingsFile(InstallScope scope, IHostEnvironment environment)
  {
    if (scope == InstallScope.User)
    {
      var configDir = environment.GetVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } dir
        ? dir
        : Path.Combine(environment.HomeDirectory, ".claude");
      return Path.Combine(configDir, "settings.json");
    }

    var projectRoot = GitContextReader.FindWorkingTreeRoot(environment.CurrentDirectory) ?? environment.CurrentDirectory;
    return Path.Combine(projectRoot, ".claude", scope == InstallScope.Local ? "settings.local.json" : "settings.json");
  }

  private static Command CreateInstall(IHostEnvironment environment, TextWriter stdout, TextWriter stderr)
  {
    var agent = AgentArgument();
    var scope = ScopeOption();
    var absolutePath = new Option<bool>("--absolute-path") { Description = "Write the full path of this agentledger binary instead of relying on PATH." };
    var command = new Command("install", "Add AgentLedger hooks for every event to the agent's settings.") { agent, scope, absolutePath };

    command.SetAction(async (parseResult, _) =>
    {
      if (!await CheckAgentAsync(parseResult.GetValue(agent)!, stderr))
      {
        return 1;
      }

      var chosen = parseResult.GetValue(scope);
      var file = SettingsFile(chosen, environment);
      var binary = parseResult.GetValue(absolutePath) && environment.ProcessPath is { } path ? Quote(path) : BinaryName;

      try
      {
        ClaudeCodeHookSettings.Install(file, $"{binary} hook {SupportedAgent}");
      }
      catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
      {
        await stderr.WriteLineAsync($"Could not update {file}: {ex.Message}");
        return 1;
      }

      await stdout.WriteLineAsync($"Installed AgentLedger hooks for {ClaudeCodeHookSettings.Events.Count} Claude Code events in {file} ({Name(chosen)} scope).");

      if (binary == BinaryName && FindOnPath(environment) is null)
      {
        await stdout.WriteLineAsync($"Warning: '{BinaryName}' was not found on PATH, so these hooks will fail with 'command not found'. "
                                    + "Add its folder to PATH, or re-run with --absolute-path.");
      }

      foreach (var other in Enum.GetValues<InstallScope>().Where(s => s != chosen))
      {
        var otherFile = SettingsFile(other, environment);
        if (otherFile != file && ClaudeCodeHookSettings.IsInstalled(otherFile))
        {
          await stdout.WriteLineAsync($"Warning: AgentLedger is also installed in {Name(other)} scope ({otherFile}), so every event would be recorded twice. "
                                      + $"Run 'agentledger uninstall {SupportedAgent} --scope {Name(other)}'.");
        }
      }

      return 0;
    });

    return command;
  }

  private static Command CreateUninstall(IHostEnvironment environment, TextWriter stdout, TextWriter stderr)
  {
    var agent = AgentArgument();
    var scope = ScopeOption();
    var command = new Command("uninstall", "Remove AgentLedger's hooks from the agent's settings. Other hooks are left alone.") { agent, scope };

    command.SetAction(async (parseResult, _) =>
    {
      if (!await CheckAgentAsync(parseResult.GetValue(agent)!, stderr))
      {
        return 1;
      }

      var chosen = parseResult.GetValue(scope);
      var file = SettingsFile(chosen, environment);
      try
      {
        var removed = ClaudeCodeHookSettings.Uninstall(file);
        await stdout.WriteLineAsync(removed > 0
          ? $"Removed {removed} AgentLedger hook entries from {file}."
          : $"AgentLedger isn't installed in {Name(chosen)} scope ({file}).");
        return 0;
      }
      catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
      {
        await stderr.WriteLineAsync($"Could not update {file}: {ex.Message}");
        return 1;
      }
    });

    return command;
  }

  private static Argument<string> AgentArgument() => new("agent") { Description = "The agent's kebab-case name. Supported: claude-code." };

  private static Option<InstallScope> ScopeOption() => new("--scope")
  {
    Description = "user: every project on this machine (default). local: this project, just you. project: this project, everyone (committed).",
    DefaultValueFactory = _ => InstallScope.User,
  };

  private static async Task<bool> CheckAgentAsync(string agent, TextWriter stderr)
  {
    if (agent == SupportedAgent)
    {
      return true;
    }

    await stderr.WriteLineAsync($"Installing hooks for '{agent}' isn't supported yet. Supported: {SupportedAgent}.");
    return false;
  }

  private static string? FindOnPath(IHostEnvironment environment)
  {
    var names = OperatingSystem.IsWindows() ? new[] { BinaryName + ".exe" } : [BinaryName];
    return (environment.GetVariable("PATH") ?? string.Empty)
      .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
      .SelectMany(dir => names.Select(name => Path.Combine(dir, name)))
      .FirstOrDefault(File.Exists);
  }

  private static string Quote(string path) => path.Contains(' ') ? $"\"{path}\"" : path;

  private static string Name(InstallScope scope) => scope.ToString().ToLowerInvariant();
}
