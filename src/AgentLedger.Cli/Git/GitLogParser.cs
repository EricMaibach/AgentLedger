using System.Globalization;

namespace AgentLedger.Cli.Git;

/// <summary>A file a commit changed: added, modified, deleted, renamed, copied or typeChanged; the old path for renames and copies.</summary>
internal sealed record ObservedFile(string Path, string Change, string? PreviousPath);

internal sealed record ObservedCommit(string Hash, IReadOnlyList<string> Parents, DateTimeOffset AuthoredAt, DateTimeOffset CommittedAt,
  string Message, IReadOnlyList<ObservedFile> Files, bool FilesTruncated);

/// <summary>
/// Reads the output of <see cref="Format"/> with <c>git log -z --name-status</c> (ADR 0021). Every field ends with a NUL,
/// so no path or message can break the parsing; each commit starts with a record separator.
/// </summary>
internal static class GitLogParser
{
  public const int MaxFiles = 1000;

  /// <summary>Hash, parents, author and committer times (strict ISO 8601), and the raw message.</summary>
  public const string Format = "%x1e%H%x00%P%x00%aI%x00%cI%x00%B%x00";

  private const char RecordSeparator = '\u001e';

  public static IReadOnlyList<ObservedCommit> Parse(string output)
  {
    var commits = new List<ObservedCommit>();
    foreach (var record in output.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
      var fields = record.Split('\0');
      if (fields.Length < 5)
      {
        continue;
      }

      var files = ParseFiles(fields.Skip(5).Select(f => f.TrimStart('\n')).Where(f => f.Length > 0).ToList(), out var truncated);
      commits.Add(new ObservedCommit(
        fields[0],
        fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries),
        DateTimeOffset.Parse(fields[2], CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(fields[3], CultureInfo.InvariantCulture),
        fields[4],
        files,
        truncated));
    }

    return commits;
  }

  // --name-status -z: "M", path; or "R100" (similarity), old path, new path. Renames and copies carry two paths.
  private static List<ObservedFile> ParseFiles(List<string> tokens, out bool truncated)
  {
    var files = new List<ObservedFile>();
    truncated = false;
    for (var i = 0; i < tokens.Count;)
    {
      var status = tokens[i];
      var twoPaths = status[0] is 'R' or 'C';
      if (i + (twoPaths ? 2 : 1) >= tokens.Count)
      {
        break; // an incomplete entry: its path is missing
      }

      var file = twoPaths
        ? new ObservedFile(tokens[i + 2], status[0] == 'R' ? "renamed" : "copied", tokens[i + 1])
        : new ObservedFile(tokens[i + 1], Change(status[0]), null);
      i += twoPaths ? 3 : 2;

      if (files.Count == MaxFiles)
      {
        truncated = true;
        break;
      }

      files.Add(file);
    }

    return files;
  }

  private static string Change(char status) => status switch
  {
    'A' => "added",
    'D' => "deleted",
    'T' => "typeChanged",
    _ => "modified", // M, and anything git may add later
  };
}
