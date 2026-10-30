using System.Text.Json;

namespace AgentLedger.Cli.Setup;

/// <summary>
/// Which file `setup` installed (ADR 0024): self-update replaces only that one, never a CLI built from source or run
/// some other way. Kept next to the CLI's other files.
/// </summary>
internal static class InstalledBinary
{
  private static string FileFor(IHostEnvironment environment) => Path.Combine(CliPaths.DataDirectory(environment), "installed.json");

  /// <summary>Records the running binary, if it is the agentledger executable (not e.g. `dotnet` running it).</summary>
  public static bool Record(IHostEnvironment environment)
  {
    if (environment.ProcessPath is not { } path || Path.GetFileName(path) is not ("agentledger" or "agentledger.exe"))
    {
      return false;
    }

    var file = FileFor(environment);
    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
    using var stream = File.Create(file);
    using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
    writer.WriteStartObject();
    writer.WriteString("path", path);
    writer.WriteEndObject();
    return true;
  }

  public static string? Read(IHostEnvironment environment)
  {
    var file = FileFor(environment);
    try
    {
      using var json = JsonDocument.Parse(File.ReadAllText(file));
      return json.RootElement.TryGetProperty("path", out var path) ? path.GetString() : null;
    }
    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
    {
      return null;
    }
  }

  /// <summary>Whether this process is the installed binary.</summary>
  public static bool IsRunning(IHostEnvironment environment) =>
    Read(environment) is { } installed && environment.ProcessPath is { } running
    && string.Equals(Path.GetFullPath(installed), Path.GetFullPath(running),
      OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
