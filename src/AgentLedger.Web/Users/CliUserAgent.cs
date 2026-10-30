namespace AgentLedger.Web.Users;

/// <summary>The CLI names itself in <c>User-Agent: agentledger-cli/&lt;version&gt;</c> (ADR 0024).</summary>
public static class CliUserAgent
{
  private const string Product = "agentledger-cli/";

  /// <summary>The CLI version a request came from, or null when it isn't from the CLI.</summary>
  public static string? VersionOf(string? userAgent)
  {
    if (userAgent is null || !userAgent.StartsWith(Product, StringComparison.OrdinalIgnoreCase))
      return null;
    var version = userAgent[Product.Length..].Split(' ', 2)[0];
    return version.Length == 0 ? null : version;
  }
}
