using System.CommandLine;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Status;

namespace AgentLedger.Cli.Setup;

/// <summary>`agentledger setup [--url …] [--code …]`: what the installers run (ADR 0024), then `status`.</summary>
internal static class SetupCommand
{
  public static Command Create(IHostEnvironment environment, TextWriter stdout)
  {
    var url = new Option<string?>("--url") { Description = "The ledger's address, as this machine reaches it (saved in the user config file)." };
    var code = new Option<string?>("--code") { Description = "A one-time code from Settings → Machines on the ledger: exchanged for this machine's own key." };
    var command = new Command("setup", "Connect this machine to a ledger and install the hooks. Run again any time; without a code the machine's key stays.")
    {
      url,
      code,
    };

    command.SetAction(async (parseResult, _) =>
    {
      using var http = CliHttp.Create(TimeSpan.FromSeconds(30)); // a person (or the installer) is waiting, not a hook
      var exitCode = await SetupRunner.RunAsync(environment, http, parseResult.GetValue(url), parseResult.GetValue(code), stdout);
      if (exitCode == 0)
      {
        await stdout.WriteLineAsync();
        await StatusCommand.RunAsync(environment, stdout);
      }

      return exitCode;
    });

    return command;
  }
}
