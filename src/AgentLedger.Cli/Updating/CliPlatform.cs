using System.Runtime.InteropServices;

namespace AgentLedger.Cli.Updating;

/// <summary>This machine's platform, named as the ledger serves CLIs (<c>/cli/linux-x64</c>, ADR 0024).</summary>
internal static class CliPlatform
{
  public static string? Current { get; } = Name();

  private static string? Name()
  {
    var os = OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsWindows() ? "win" : null;
    var arch = RuntimeInformation.OSArchitecture switch
    {
      Architecture.X64 => "x64",
      Architecture.Arm64 => "arm64",
      _ => null,
    };
    return os is null || arch is null ? null : $"{os}-{arch}";
  }
}
