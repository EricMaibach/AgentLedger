using System.CommandLine;
using AgentLedger.Cli.Flush;
using AgentLedger.Cli.Hook;
using AgentLedger.Cli.Install;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Status;

namespace AgentLedger.Cli;

/// <summary>The command line, with the environment and console streams passed in so tests can run it in-process.</summary>
internal static class CliApp
{
  public static async Task<int> RunAsync(string[] args, IHostEnvironment environment, TextReader stdin, TextWriter stdout, TextWriter stderr)
  {
    var root = new RootCommand(
      "AgentLedger: records what AI coding agents do. Agents run 'agentledger hook <agent>' from their hooks; use 'status' to check the setup.")
    {
      HookCommand.Create(environment, stdin),
      StatusCommand.Create(environment, stdout),
      FlushCommand.Create(environment, stdout),
    };

    foreach (var command in InstallCommands.Create(environment, stdout, stderr))
    {
      root.Subcommands.Add(command);
    }

    var parseResult = root.Parse(args);

    // A misconfigured hook (e.g. missing agent) must still be silent and exit 0; log it instead.
    if (args is [HookCommand.Name, ..] && parseResult.Errors.Count > 0)
    {
      new FileLog(CliPaths.LogFile(environment), TimeProvider.System)
        .Error($"hook called with invalid arguments '{string.Join(' ', args)}': {string.Join("; ", parseResult.Errors.Select(e => e.Message))}");
      return 0;
    }

    return await parseResult.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr });
  }
}
