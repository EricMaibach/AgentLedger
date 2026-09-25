namespace AgentLedger.Cli.Envelope;

// Reads branch, origin and worktree straight from the .git files. No `git` process: spawning one
// costs 5-20 ms per call on every hook, and git may not even be installed (ADR 0012).
// Never throws: a broken repository must never break the agent's hook.
internal static class GitContextReader
{
  public static GitContext Read(string startDirectory)
  {
    try
    {
      var workingTree = FindWorkingTreeRoot(startDirectory);
      if (workingTree is null)
      {
        return GitContext.None;
      }

      var (gitDir, commonDir, linkedWorktree) = FindGitDirectories(workingTree);
      var origin = GitConfigFile.Read(Path.Combine(commonDir, "config"), "remote \"origin\"", "url");

      return new GitContext(
        Repo: origin is null ? null : NormalizeRemote(origin),
        Branch: ReadBranch(Path.Combine(gitDir, "HEAD")),
        Worktree: linkedWorktree);
    }
    catch (Exception ex) when (IsUnreadableRepository(ex))
    {
      return GitContext.None;
    }
  }

  /// <summary>The git config file holding repository settings (the shared one, for linked worktrees), or null.</summary>
  public static string? FindConfigFile(string startDirectory)
  {
    try
    {
      var workingTree = FindWorkingTreeRoot(startDirectory);
      return workingTree is null ? null : Path.Combine(FindGitDirectories(workingTree).CommonDir, "config");
    }
    catch (Exception ex) when (IsUnreadableRepository(ex))
    {
      return null;
    }
  }

  /// <summary>Walks up from the start directory to the first folder containing .git (a folder or a file).</summary>
  public static string? FindWorkingTreeRoot(string startDirectory)
  {
    for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
    {
      var dotGit = Path.Combine(dir.FullName, ".git");
      if (Directory.Exists(dotGit) || File.Exists(dotGit))
      {
        return dir.FullName;
      }
    }

    return null;
  }

  // GitDir holds this working tree's HEAD; CommonDir holds the shared config. They differ only for a
  // linked worktree, whose .git is a file: "gitdir: <main>/.git/worktrees/<name>", with a commondir file inside.
  private static (string GitDir, string CommonDir, string? LinkedWorktree) FindGitDirectories(string workingTree)
  {
    var dotGit = Path.Combine(workingTree, ".git");
    if (Directory.Exists(dotGit))
    {
      return (dotGit, dotGit, null);
    }

    var pointer = File.ReadAllText(dotGit).Trim();
    if (!pointer.StartsWith("gitdir:", StringComparison.Ordinal))
    {
      throw new InvalidDataException($"Unrecognized .git file in {workingTree}.");
    }

    var gitDir = Path.GetFullPath(pointer["gitdir:".Length..].Trim(), workingTree);
    var commonDirFile = Path.Combine(gitDir, "commondir");
    var commonDir = File.Exists(commonDirFile)
      ? Path.GetFullPath(File.ReadAllText(commonDirFile).Trim(), gitDir)
      : gitDir;

    return (gitDir, commonDir, workingTree);
  }

  // HEAD is "ref: refs/heads/<branch>" on a branch, or a bare commit hash when detached.
  private static string? ReadBranch(string headPath)
  {
    const string prefix = "ref: refs/heads/";
    if (!File.Exists(headPath))
    {
      return null;
    }

    var head = File.ReadAllText(headPath).Trim();
    return head.StartsWith(prefix, StringComparison.Ordinal) ? head[prefix.Length..] : null;
  }

  private static bool IsUnreadableRepository(Exception ex) =>
    ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException;

  /// <summary>
  /// Reduces a remote URL to <c>host/path</c>: strips credentials (remotes can embed tokens, e.g.
  /// <c>https://ghp_…@github.com/…</c>) and makes the HTTPS and SSH forms of one repository identical.
  /// </summary>
  public static string NormalizeRemote(string remote)
  {
    remote = remote.Trim();
    string host, path;

    if (Uri.TryCreate(remote, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
    {
      // https://…, ssh://…, git://… (Uri keeps any user:token in UserInfo, which we drop)
      host = uri.Host;
      path = uri.AbsolutePath;
    }
    else if (remote.IndexOf(':') is var colon and > 0 && !remote.Contains("://", StringComparison.Ordinal))
    {
      // scp-like SSH: [user@]host:owner/repo.git
      var hostPart = remote[..colon];
      host = hostPart[(hostPart.LastIndexOf('@') + 1)..];
      path = remote[(colon + 1)..];
    }
    else
    {
      return remote; // a local path or something unrecognized; nothing to strip
    }

    path = path.Trim('/');
    if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
    {
      path = path[..^4];
    }

    return $"{host}/{path}";
  }
}
