using System.CommandLine;
using System.Text;
using AgentLedger.Cli.Configuration;

namespace AgentLedger.Cli.Login;

/// <summary>`agentledger login [--key …]`: stores an ingest key for this user (ADR 0019).</summary>
internal static class LoginCommand
{
  public static Command Create(IHostEnvironment environment, TextReader stdin, TextWriter stdout)
  {
    var key = new Option<string?>("--key") { Description = "The ingest key. Without it, you're asked for it (typing isn't shown), or it's read from piped input." };
    var command = new Command("login", "Store an API key for sending events, after checking it with the ledger.") { key };

    command.SetAction(async (parseResult, _) =>
    {
      var settings = new SettingsLoader(environment).Load(environment.CurrentDirectory);
      var apiKey = parseResult.GetValue(key) ?? await ReadKeyAsync(stdin, stdout);
      using var http = new HttpClient { Timeout = settings.Timeout.Value + TimeSpan.FromSeconds(4) }; // a person is waiting, not a hook
      return await LoginRunner.RunAsync(environment, http, settings.Url.Value, apiKey, stdout);
    });

    return command;
  }

  // Asking without echo keeps the key out of the terminal's scrollback; --key would put it in shell history.
  private static async Task<string?> ReadKeyAsync(TextReader stdin, TextWriter stdout)
  {
    if (!ReferenceEquals(stdin, Console.In) || Console.IsInputRedirected)
    {
      return await stdin.ReadLineAsync();
    }

    await stdout.WriteAsync("API key (input hidden): ");
    var typed = new StringBuilder();
    for (var pressed = Console.ReadKey(intercept: true); pressed.Key != ConsoleKey.Enter; pressed = Console.ReadKey(intercept: true))
    {
      if (pressed.Key == ConsoleKey.Backspace)
      {
        typed.Length = Math.Max(0, typed.Length - 1);
      }
      else if (!char.IsControl(pressed.KeyChar))
      {
        typed.Append(pressed.KeyChar);
      }
    }

    await stdout.WriteLineAsync();
    return typed.ToString();
  }
}
