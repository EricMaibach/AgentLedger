using System.Reflection;

namespace AgentLedger.Cli;

/// <summary>This CLI's version, as built (e.g. <c>0.6.0+9ad4c11…</c>), matching the ledger it was released with (ADR 0024).</summary>
internal static class CliVersion
{
  public static string Current { get; } =
    typeof(CliVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

  /// <summary>The version without its build part (after '+'): what decides whether two versions are the same release.</summary>
  public static string Release(string version) => version.Split('+', 2)[0];
}
