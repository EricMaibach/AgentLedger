using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgentLedger.Cli.Transcripts;

/// <summary>
/// Where each transcript was read up to (ADR 0020), one small file per transcript next to the spool, plus a
/// lock file: each hook is its own process and several run at once, so a second one finds the lock taken and
/// skips that file (the next hook catches up).
/// </summary>
internal sealed class TranscriptStateStore(string directory)
{
  public TranscriptFileState? Load(string path)
  {
    var file = StatePath(path);
    if (!File.Exists(file))
    {
      return null;
    }

    try
    {
      using var json = JsonDocument.Parse(File.ReadAllText(file));
      var root = json.RootElement;
      return new TranscriptFileState(
        root.GetProperty("path").GetString()!,
        root.GetProperty("offset").GetInt64(),
        root.GetProperty("fingerprintLength").GetInt32(),
        root.GetProperty("fingerprint").GetString()!);
    }
    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
    {
      return null; // unreadable: start again; the ledger recognizes lines it already has
    }
  }

  public void Save(TranscriptFileState state)
  {
    EnsureDirectory();
    using var buffer = new MemoryStream();
    using (var writer = new Utf8JsonWriter(buffer))
    {
      writer.WriteStartObject();
      writer.WriteString("path", state.Path);
      writer.WriteNumber("offset", state.Offset);
      writer.WriteNumber("fingerprintLength", state.FingerprintLength);
      writer.WriteString("fingerprint", state.Fingerprint);
      writer.WriteEndObject();
    }

    // Write then rename: a reader never sees half a file.
    var target = StatePath(state.Path);
    var temp = $"{target}.tmp-{Guid.NewGuid():N}";
    File.WriteAllBytes(temp, buffer.ToArray());
    File.Move(temp, target, overwrite: true);
  }

  /// <summary>An exclusive lock on reading this transcript, or null if another process holds it.</summary>
  public IDisposable? TryLock(string path)
  {
    EnsureDirectory();
    try
    {
      return new FileStream(Path.ChangeExtension(StatePath(path), ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    catch (IOException)
    {
      return null;
    }
  }

  public int Count() => Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.json").Count() : 0;

  // Named by a hash of the path: paths contain separators and can be long.
  private string StatePath(string path) =>
    Path.Combine(directory, $"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..32]}.json");

  private void EnsureDirectory()
  {
    if (OperatingSystem.IsWindows())
    {
      Directory.CreateDirectory(directory);
    }
    else
    {
      Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
  }
}
