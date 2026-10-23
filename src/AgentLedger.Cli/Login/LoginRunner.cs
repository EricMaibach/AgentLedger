using AgentLedger.Cli.Configuration;

namespace AgentLedger.Cli.Login;

/// <summary>`agentledger login`: checks an ingest key with the ledger, then stores it in the user config file (ADR 0019).</summary>
internal static class LoginRunner
{
  private const string IngestPrefix = "agl_ingest_";
  private const string ReadPrefix = "agl_read_";

  /// <summary>Returns the exit code: 0 saved, 1 refused or couldn't check, 2 not a key.</summary>
  public static async Task<int> RunAsync(IHostEnvironment environment, HttpClient http, Uri apiUrl, string? apiKey, TextWriter output)
  {
    var key = apiKey?.Trim() ?? "";
    if (key.StartsWith(ReadPrefix, StringComparison.Ordinal))
    {
      await output.WriteLineAsync("That's a read key. The CLI sends events, which needs an ingest key: create one under Settings → API keys.");
      return 1;
    }

    if (!key.StartsWith(IngestPrefix, StringComparison.Ordinal))
    {
      await output.WriteLineAsync($"That isn't an AgentLedger API key: they start with {IngestPrefix} or {ReadPrefix}.");
      return 2;
    }

    var check = await ApiKeyCheck.CheckAsync(http, apiUrl, key);
    switch (check.Result)
    {
      case ApiKeyCheckResult.Rejected:
        await output.WriteLineAsync($"The ledger at {apiUrl} rejected this key (unknown, revoked or expired). Nothing saved.");
        return 1;
      case ApiKeyCheckResult.Unknown:
        await output.WriteLineAsync($"Couldn't check the key with the ledger at {apiUrl}, so nothing was saved: {check.Detail}.");
        return 1;
    }

    UserConfigFile.SetApiKey(environment, key);
    await output.WriteLineAsync($"Saved to {UserConfigFile.PathFor(environment)}: '{check.Name}' ({check.Scope}, owner {check.Owner}).");
    if (environment.GetVariable("AGENTLEDGER_API_KEY") is { Length: > 0 })
    {
      await output.WriteLineAsync("Note: AGENTLEDGER_API_KEY is set, and it takes precedence over the saved key.");
    }

    return 0;
  }
}
