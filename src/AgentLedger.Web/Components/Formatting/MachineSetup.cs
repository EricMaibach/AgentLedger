namespace AgentLedger.Web.Components.Formatting;

/// <summary>What the machines page shows (ADR 0024): install commands, address choices, CLI versions.</summary>
public static class MachineSetup
{
  private const string DevContainerHost = "host.docker.internal";

  /// <summary>Linux and macOS: download the installer from the ledger and run it with the code.</summary>
  public static string UnixCommand(string address, string code) =>
    $"curl -fsSL {Trim(address)}/install.sh | sh -s -- --code {code}";

  /// <summary>Windows PowerShell: the code goes in an environment variable, since a piped script takes no arguments.</summary>
  public static string WindowsCommand(string address, string code) =>
    $"$env:AGENTLEDGER_CODE='{code}'; irm {Trim(address)}/install.ps1 | iex";

  /// <summary>For people who read a script before running it.</summary>
  public static IReadOnlyList<string> ReadFirstCommands(string address, string code) =>
  [
    $"curl -fsSLo install.sh {Trim(address)}/install.sh",
    "less install.sh",
    $"sh install.sh --code {code}",
  ];

  /// <summary>
  /// The address a machine should use: the one the page is browsed at, and the usual one from a dev container on the
  /// same server (Docker's host.docker.internal, with the same port).
  /// </summary>
  public static IReadOnlyList<(string Label, string Address)> AddressChoices(Uri browsing)
  {
    var here = browsing.GetLeftPart(UriPartial.Authority);
    if (browsing.Host == DevContainerHost)
      return [("This address", here)];
    var devContainer = new UriBuilder(browsing.Scheme, DevContainerHost, browsing.IsDefaultPort ? -1 : browsing.Port).Uri
      .GetLeftPart(UriPartial.Authority);
    return [("This address", here), ("From a dev container on this server", devContainer)];
  }

  /// <summary>A typed address as http(s)://host[:port], or null when it isn't one.</summary>
  public static string? NormalizeAddress(string? typed)
  {
    if (!Uri.TryCreate(typed?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
        || uri.AbsolutePath.TrimEnd('/').Length > 0 || uri.Query.Length > 0)
      return null;
    return uri.GetLeftPart(UriPartial.Authority);
  }

  /// <summary>
  /// Whether a machine's CLI will update itself: CLIs follow the ledger's version, older or newer (ADR 0024). The build
  /// part after '+' is ignored. Unknown versions (nothing sent yet) don't count.
  /// </summary>
  public static bool NeedsUpdate(string? cliVersion, string ledgerVersion) =>
    cliVersion is not null && Core(cliVersion) != Core(ledgerVersion);

  /// <summary>A version without its build part: <c>0.6.0+9ad4c11…</c> → <c>0.6.0</c>.</summary>
  public static string Release(string version) => version.Split('+', 2)[0];

  private static string Core(string version) => Release(version);

  private static string Trim(string address) => address.TrimEnd('/');
}
