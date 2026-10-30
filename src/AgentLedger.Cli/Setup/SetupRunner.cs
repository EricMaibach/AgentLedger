using System.Net;
using System.Text;
using System.Text.Json;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Envelope;
using AgentLedger.Cli.Install;

namespace AgentLedger.Cli.Setup;

/// <summary>
/// `agentledger setup [--url] [--code]` (ADR 0024): what the installers run. Saves the ledger's address, exchanges a
/// one-time code for this machine's own ingest key, records the installed binary and installs the Claude Code hooks.
/// Safe to run again: without a code, the machine's key stays.
/// </summary>
internal static class SetupRunner
{
  /// <summary>Returns the exit code: 0 set up, 1 the code wasn't exchanged, 2 not a web address.</summary>
  public static async Task<int> RunAsync(IHostEnvironment environment, HttpClient http, string? url, string? code, TextWriter output)
  {
    if (url is not null)
    {
      if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
      {
        await output.WriteLineAsync($"'{url}' isn't a web address: use http://host:port or https://host.");
        return 2;
      }

      UserConfigFile.Set(environment, "url", uri.GetLeftPart(UriPartial.Authority));
    }

    var settings = new SettingsLoader(environment).Load(environment.CurrentDirectory);
    var ledger = settings.Url.Value;
    await output.WriteLineAsync($"Ledger: {ledger}");

    if (!string.IsNullOrWhiteSpace(code))
    {
      var hadKey = settings.ApiKey.Value is not null;
      if (await EnrollAsync(environment, http, ledger, code.Trim(), output) is not { } keyName)
      {
        return 1;
      }

      await output.WriteLineAsync($"Connected: this machine sends with its own key, '{keyName}'.");
      if (hadKey)
      {
        await output.WriteLineAsync("Its earlier key stays valid until you revoke it under Settings on the ledger.");
      }
    }
    else if (settings.ApiKey.Value is null)
    {
      await output.WriteLineAsync("Not connected yet: create a code under Settings → Machines on the ledger, then run: agentledger setup --code <code>");
    }

    InstalledBinary.Record(environment);
    await InstallHooksAsync(environment, output);
    return 0;
  }

  // The machine's new key, saved; null (with the reason written) when the ledger didn't give one.
  private static async Task<string?> EnrollAsync(IHostEnvironment environment, HttpClient http, Uri ledger, string code, TextWriter output)
  {
    using var body = new MemoryStream();
    using (var writer = new Utf8JsonWriter(body))
    {
      writer.WriteStartObject();
      writer.WriteString("code", code);
      writer.WriteString("host", HostName.For(environment));
      writer.WriteEndObject();
    }

    try
    {
      using var response = await http.PostAsync(new Uri(ledger.ToString().TrimEnd('/') + "/enroll"),
        new StringContent(Encoding.UTF8.GetString(body.ToArray()), Encoding.UTF8, "application/json"));
      var text = await response.Content.ReadAsStringAsync();
      if (response.StatusCode != HttpStatusCode.OK)
      {
        await output.WriteLineAsync($"The ledger didn't accept the code: {Reason(text) ?? $"{(int)response.StatusCode} {response.ReasonPhrase}"}");
        return null;
      }

      using var json = JsonDocument.Parse(text);
      var key = json.RootElement.GetProperty("apiKey").GetString()!;
      UserConfigFile.SetApiKey(environment, key);
      return json.RootElement.GetProperty("keyName").GetString();
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
    {
      await output.WriteLineAsync($"Couldn't reach the ledger at {ledger}: {ex.Message}");
      return null;
    }
  }

  // The first validation message of a problem answer, else its title.
  private static string? Reason(string problem)
  {
    try
    {
      using var json = JsonDocument.Parse(problem);
      var root = json.RootElement;
      if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
      {
        foreach (var field in errors.EnumerateObject())
        {
          if (field.Value.ValueKind == JsonValueKind.Array && field.Value.GetArrayLength() > 0)
            return field.Value[0].GetString();
        }
      }

      return root.TryGetProperty("title", out var title) ? title.GetString() : null;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  // The user-scope hooks (ADR 0013). By name when it's on PATH, else by full path: agents' hooks may not get the shell's
  // PATH, and a bare name would fail.
  private static async Task InstallHooksAsync(IHostEnvironment environment, TextWriter output)
  {
    var file = InstallCommands.SettingsFile(InstallScope.User, environment);
    var binary = InstallCommands.FindOnPath(environment) is null && environment.ProcessPath is { } path
      ? InstallCommands.Quote(path)
      : "agentledger";
    try
    {
      ClaudeCodeHookSettings.Install(file, $"{binary} hook claude-code");
      await output.WriteLineAsync($"Hooks: Claude Code, in {file}");
    }
    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
    {
      await output.WriteLineAsync($"Could not update {file}: {ex.Message}");
    }
  }
}
