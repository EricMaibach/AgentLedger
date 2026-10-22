using System.Reflection;

namespace AgentLedger.Web;

/// <summary>The running build: release version plus commit, e.g. "0.2.0+3f7a9c1…" (stamped in by the release workflow).</summary>
public static class BuildInfo
{
  public static string Version { get; } =
    typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
