using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Setup;

namespace AgentLedger.Cli.Updating;

internal enum UpdateOutcome
{
  /// <summary>The ledger's version is this CLI's, or unknown.</summary>
  NotNeeded,

  /// <summary>autoUpdate is false.</summary>
  TurnedOff,

  /// <summary>This isn't the binary `setup` installed (e.g. built from source): never replaced.</summary>
  NotInstalled,

  /// <summary>Tried less than an hour ago.</summary>
  TooSoon,

  Updated,
  Failed,
}

/// <summary>
/// The installed CLI follows the ledger's version (ADR 0024), older or newer. Run after a hook's normal work, so the
/// event is already safe: at most one attempt an hour, the download checked against the ledger's checksum, the file
/// replaced atomically (a hook running meanwhile uses the old file or the new one, never half of one), then the hooks
/// refreshed. Anything going wrong is logged; the old CLI keeps working.
/// </summary>
internal sealed class SelfUpdater(IHostEnvironment environment, HttpClient http, TimeProvider clock, ICliLog log,
  Func<string, Task> refreshHooks)
{
  public static readonly TimeSpan MinimumInterval = TimeSpan.FromHours(1);

  public async Task<UpdateOutcome> MaybeUpdateAsync(CliSettings settings, string? ledgerVersion, CancellationToken cancellationToken)
  {
    if (ledgerVersion is null || CliVersion.Release(ledgerVersion) == CliVersion.Release(CliVersion.Current))
      return UpdateOutcome.NotNeeded;
    if (!settings.AutoUpdate.Value)
      return UpdateOutcome.TurnedOff;
    if (!InstalledBinary.IsRunning(environment) || environment.ProcessPath is not { } binary)
      return UpdateOutcome.NotInstalled;

    var now = clock.GetUtcNow();
    if (LastAttempt() is { } last && now - last < MinimumInterval)
      return UpdateOutcome.TooSoon;
    RecordAttempt(now);

    var temporary = Path.Combine(Path.GetDirectoryName(binary)!, $".{Path.GetFileName(binary)}.new.{Environment.ProcessId}");
    try
    {
      if (CliPlatform.Current is not { } platform)
      {
        log.Error("Self-update: this platform has no CLI download.");
        return UpdateOutcome.Failed;
      }

      var download = new Uri($"{settings.Url.Value.ToString().TrimEnd('/')}/cli/{platform}");
      var bytes = await http.GetByteArrayAsync(download, cancellationToken);
      var expected = (await http.GetStringAsync(new Uri(download + ".sha256"), cancellationToken)).Split(' ', 2)[0].Trim();
      if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), expected, StringComparison.OrdinalIgnoreCase))
      {
        log.Error($"Self-update to {ledgerVersion}: the download from {download} doesn't match its checksum; kept {CliVersion.Current}.");
        return UpdateOutcome.Failed;
      }

      await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
      Replace(binary, temporary);
      log.Info($"Updated the CLI from {CliVersion.Current} to {ledgerVersion} (the ledger's version).");
      await refreshHooks(binary);
      return UpdateOutcome.Updated;
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
    {
      log.Error($"Self-update to {ledgerVersion} failed; kept {CliVersion.Current}: {ex.Message}");
      return UpdateOutcome.Failed;
    }
    finally
    {
      File.Delete(temporary);
    }
  }

  // Unix: a rename over the running file is atomic and allowed. Windows won't replace a running .exe, but lets it be
  // renamed: the old one is moved aside first and removed by a later run.
  private static void Replace(string binary, string replacement)
  {
    if (OperatingSystem.IsWindows())
    {
      var aside = binary + ".old";
      if (File.Exists(aside))
        File.Delete(aside);
      File.Move(binary, aside);
      File.Move(replacement, binary);
      return;
    }

    File.SetUnixFileMode(replacement, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
      | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    File.Move(replacement, binary, overwrite: true);
  }

  private string StateFile => Path.Combine(CliPaths.DataDirectory(environment), "update.json");

  private DateTimeOffset? LastAttempt()
  {
    try
    {
      using var json = JsonDocument.Parse(File.ReadAllText(StateFile));
      return DateTimeOffset.Parse(json.RootElement.GetProperty("lastAttempt").GetString()!, CultureInfo.InvariantCulture);
    }
    catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or FormatException or InvalidOperationException
                                 or UnauthorizedAccessException)
    {
      return null;
    }
  }

  private void RecordAttempt(DateTimeOffset now)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
    using var stream = File.Create(StateFile);
    using var writer = new Utf8JsonWriter(stream);
    writer.WriteStartObject();
    writer.WriteString("lastAttempt", now.ToString("O", CultureInfo.InvariantCulture));
    writer.WriteEndObject();
  }

  /// <summary>Refreshes the hooks with the new binary: `setup` without a code keeps the key and reinstalls them.</summary>
  public static async Task RunSetupAsync(string binary)
  {
    using var process = Process.Start(new ProcessStartInfo(binary, "setup")
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
    });
    if (process is null)
      return;
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    _ = process.StandardOutput.ReadToEndAsync(timeout.Token);
    _ = process.StandardError.ReadToEndAsync(timeout.Token);
    await process.WaitForExitAsync(timeout.Token);
  }
}
