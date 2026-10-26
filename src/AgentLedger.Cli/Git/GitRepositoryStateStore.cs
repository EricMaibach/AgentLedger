using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgentLedger.Cli.Git;

/// <summary>
/// What the CLI last saw in a working tree (ADR 0021): its HEAD, and when it first saw the repository (the
/// baseline: older commits aren't sent).
/// </summary>
internal sealed record GitRepositoryState(string WorkingTree, string Head, DateTimeOffset BaselineAt);

/// <summary>
/// A repository's tags as last seen, kept by its shared git folder (worktrees share tags), with a fingerprint of the
/// tag files to notice changes without running git.
/// </summary>
internal sealed record GitTagState(string CommonDirectory, string Fingerprint, IReadOnlyDictionary<string, string> Tags);

/// <summary>One small file per working tree next to the spool, plus a lock so parallel hooks skip rather than collide.</summary>
internal sealed class GitRepositoryStateStore(string directory)
{
  public GitRepositoryState? Load(string workingTree)
  {
    var file = StatePath(workingTree);
    if (!File.Exists(file))
    {
      return null;
    }

    try
    {
      using var json = JsonDocument.Parse(File.ReadAllText(file));
      var root = json.RootElement;
      return new GitRepositoryState(
        root.GetProperty("workingTree").GetString()!,
        root.GetProperty("head").GetString()!,
        DateTimeOffset.Parse(root.GetProperty("baselineAt").GetString()!, CultureInfo.InvariantCulture));
    }
    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
    {
      return null; // unreadable: a new baseline
    }
  }

  public void Save(GitRepositoryState state)
  {
    EnsureDirectory();
    using var buffer = new MemoryStream();
    using (var writer = new Utf8JsonWriter(buffer))
    {
      writer.WriteStartObject();
      writer.WriteString("workingTree", state.WorkingTree);
      writer.WriteString("head", state.Head);
      writer.WriteString("baselineAt", state.BaselineAt.ToString("O", CultureInfo.InvariantCulture));
      writer.WriteEndObject();
    }

    // Write then rename: a reader never sees half a file.
    var target = StatePath(state.WorkingTree);
    var temp = $"{target}.tmp-{Guid.NewGuid():N}";
    File.WriteAllBytes(temp, buffer.ToArray());
    File.Move(temp, target, overwrite: true);
  }

  public GitTagState? LoadTags(string commonDirectory)
  {
    var file = TagStatePath(commonDirectory);
    if (!File.Exists(file))
    {
      return null;
    }

    try
    {
      using var json = JsonDocument.Parse(File.ReadAllText(file));
      var root = json.RootElement;
      var tags = root.GetProperty("tags").EnumerateObject().ToDictionary(t => t.Name, t => t.Value.GetString()!);
      return new GitTagState(root.GetProperty("commonDirectory").GetString()!, root.GetProperty("fingerprint").GetString()!, tags);
    }
    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
    {
      return null; // unreadable: a new baseline
    }
  }

  public void SaveTags(GitTagState state)
  {
    EnsureDirectory();
    using var buffer = new MemoryStream();
    using (var writer = new Utf8JsonWriter(buffer))
    {
      writer.WriteStartObject();
      writer.WriteString("commonDirectory", state.CommonDirectory);
      writer.WriteString("fingerprint", state.Fingerprint);
      writer.WriteStartObject("tags");
      foreach (var (name, commit) in state.Tags)
      {
        writer.WriteString(name, commit);
      }

      writer.WriteEndObject();
      writer.WriteEndObject();
    }

    var target = TagStatePath(state.CommonDirectory);
    var temp = $"{target}.tmp-{Guid.NewGuid():N}";
    File.WriteAllBytes(temp, buffer.ToArray());
    File.Move(temp, target, overwrite: true);
  }

  /// <summary>An exclusive lock on checking this working tree, or null if another process holds it.</summary>
  public IDisposable? TryLock(string workingTree) => TryLockFile(Path.ChangeExtension(StatePath(workingTree), ".lock"));

  /// <summary>An exclusive lock on checking this repository's tags, or null if another process holds it.</summary>
  public IDisposable? TryLockTags(string commonDirectory) => TryLockFile(Path.ChangeExtension(TagStatePath(commonDirectory), ".lock"));

  // Working trees watched for commits (tag state files are named separately).
  public int Count() => Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.json").Count(f => !Path.GetFileName(f).StartsWith("tags-", StringComparison.Ordinal)) : 0;

  private IDisposable? TryLockFile(string path)
  {
    EnsureDirectory();
    try
    {
      return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    catch (IOException)
    {
      return null;
    }
  }

  private string StatePath(string workingTree) => Path.Combine(directory, $"{Key(workingTree)}.json");

  private string TagStatePath(string commonDirectory) => Path.Combine(directory, $"tags-{Key(commonDirectory)}.json");

  private static string Key(string path) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..32];

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
