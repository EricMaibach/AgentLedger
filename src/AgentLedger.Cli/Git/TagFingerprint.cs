using System.Security.Cryptography;
using System.Text;

namespace AgentLedger.Cli.Git;

/// <summary>
/// A fingerprint of a repository's tag files (ADR 0021): every file under refs/tags (name, size, modification time)
/// and packed-refs. If it hasn't changed, the tags haven't either, and git needn't run. Packing changes it without
/// changing the tags; the comparison with the saved tags then simply finds nothing.
/// </summary>
internal static class TagFingerprint
{
  public static string Of(string commonDirectory)
  {
    var entries = new List<string>();
    var tagsFolder = Path.Combine(commonDirectory, "refs", "tags");
    if (Directory.Exists(tagsFolder))
    {
      foreach (var file in Directory.EnumerateFiles(tagsFolder, "*", SearchOption.AllDirectories))
      {
        var info = new FileInfo(file);
        entries.Add($"{Path.GetRelativePath(tagsFolder, file)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
      }
    }

    var packedRefs = new FileInfo(Path.Combine(commonDirectory, "packed-refs"));
    if (packedRefs.Exists)
    {
      entries.Add($"packed-refs|{packedRefs.Length}|{packedRefs.LastWriteTimeUtc.Ticks}");
    }

    entries.Sort(StringComparer.Ordinal);
    return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', entries))));
  }
}
