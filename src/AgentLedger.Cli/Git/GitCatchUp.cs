using System.Globalization;
using AgentLedger.Cli.Envelope;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Git;

/// <summary>
/// A tag change as observed: existing (at the baseline), created, moved or deleted. The ID is assigned here, when the
/// change is seen, so the ledger recognizes the same batch sent twice (ADR 0021, revised).
/// </summary>
internal sealed record ObservedTag(Guid Id, string Name, string Change, string? Commit, string? PreviousCommit, DateTimeOffset ObservedAt);

/// <summary>
/// What each hook does for git (ADR 0021): new commits, then tag changes. Both are checked from the .git files
/// first; git only runs when HEAD moved or the tag files changed. Spool first, then save the state, so a repeat
/// after a crash is harmless.
/// </summary>
internal sealed class GitCatchUp(IGitRunner git, GitRepositoryStateStore states, EventSpool spool, ICliLog log, TimeProvider clock,
  string machineName, int maxCommits = GitCatchUp.DefaultMaxCommits)
{
  public const int DefaultMaxCommits = 200;
  public const int MaxTagsPerBatch = 1000;

  private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(2);

  public async Task RunAsync(string projectDirectory)
  {
    var workingTree = GitContextReader.FindWorkingTreeRoot(projectDirectory);
    var commonDirectory = GitContextReader.FindCommonDirectory(projectDirectory);
    var context = GitContextReader.Read(projectDirectory);
    if (workingTree is null || commonDirectory is null || context.Commit is not { } head)
    {
      return; // not a repository, or no commits yet
    }

    var repository = context.Repo ?? $"{machineName}:{workingTree}";
    await CatchUpCommitsAsync(workingTree, head, repository);
    await CatchUpTagsAsync(workingTree, commonDirectory, repository);
  }

  // Commits: the ones since the last HEAD seen in this working tree, made after the baseline, oldest first,
  // at most maxCommits per run.
  private async Task CatchUpCommitsAsync(string workingTree, string head, string repository)
  {
    using var repositoryLock = states.TryLock(workingTree);
    if (repositoryLock is null)
    {
      return; // another hook is checking it; the next one catches up
    }

    var state = states.Load(workingTree);
    if (state is null)
    {
      states.Save(new GitRepositoryState(workingTree, head, clock.GetUtcNow())); // first sight: a baseline, no history
      return;
    }

    if (state.Head == head)
    {
      return;
    }

    var since = state.BaselineAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    var range = await git.RunAsync(workingTree, ["rev-list", "--reverse", $"--since={since}", $"{state.Head}..{head}"], GitTimeout);
    if (range.ExitCode != 0)
    {
      // Git missing, too slow, or the old HEAD is gone (e.g. after a rebase and gc): carry on from here.
      log.Error($"Couldn't list new commits with git in {workingTree} (exit code {range.ExitCode}); continuing from {head}.");
      states.Save(state with { Head = head });
      return;
    }

    var hashes = range.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(maxCommits).ToList();
    if (hashes.Count == 0)
    {
      states.Save(state with { Head = head }); // HEAD moved without new commits (e.g. back, or to older history)
      return;
    }

    var details = await git.RunAsync(workingTree,
      ["-c", "core.quotePath=false", "log", "--no-walk=unsorted", "-z", "-M", "--name-status", $"--format={GitLogParser.Format}", .. hashes],
      GitTimeout);
    if (details.ExitCode != 0)
    {
      log.Error($"Couldn't read new commits with git in {workingTree} (exit code {details.ExitCode}); will try again.");
      return;
    }

    Spool(GitBatches.ToJson(repository, GitLogParser.Parse(details.Output), []));

    // A long run continues next time from the last commit sent.
    states.Save(state with { Head = hashes.Count < maxCommits ? head : hashes[^1] });
  }

  // Tags: kept by the shared git folder (worktrees share them); listed only when the tag files changed, and
  // compared with the last list. At first sight, every existing tag.
  private async Task CatchUpTagsAsync(string workingTree, string commonDirectory, string repository)
  {
    using var tagsLock = states.TryLockTags(commonDirectory);
    if (tagsLock is null)
    {
      return;
    }

    var state = states.LoadTags(commonDirectory);
    var fingerprint = TagFingerprint.Of(commonDirectory);
    if (state?.Fingerprint == fingerprint)
    {
      return;
    }

    var listed = await git.RunAsync(workingTree,
      ["for-each-ref", "refs/tags", "--format=%(refname:strip=2)%00%(objectname)%00%(*objectname)"], GitTimeout);
    if (listed.ExitCode != 0)
    {
      log.Error($"Couldn't list tags with git in {workingTree} (exit code {listed.ExitCode}); will try again.");
      return;
    }

    var current = ParseTags(listed.Output);
    var now = clock.GetUtcNow();
    var observed = Compare(state?.Tags, current, now);
    foreach (var batch in observed.Chunk(MaxTagsPerBatch))
    {
      Spool(GitBatches.ToJson(repository, [], batch));
    }

    states.SaveTags(new GitTagState(commonDirectory, fingerprint, current));
  }

  // "name\0object\0peeled": an annotated tag is an object of its own, so its commit is the peeled one.
  private static Dictionary<string, string> ParseTags(string output)
  {
    var tags = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
      if (line.Split('\0') is [var name, var target, var peeled])
      {
        tags[name] = peeled.Length > 0 ? peeled : target;
      }
    }

    return tags;
  }

  private static List<ObservedTag> Compare(IReadOnlyDictionary<string, string>? before, Dictionary<string, string> now, DateTimeOffset at)
  {
    if (before is null)
    {
      return [.. now.Select(t => new ObservedTag(Guid.CreateVersion7(), t.Key, "existing", t.Value, null, at))];
    }

    var observed = new List<ObservedTag>();
    foreach (var (name, commit) in now)
    {
      if (!before.TryGetValue(name, out var previous))
      {
        observed.Add(new ObservedTag(Guid.CreateVersion7(), name, "created", commit, null, at));
      }
      else if (previous != commit)
      {
        observed.Add(new ObservedTag(Guid.CreateVersion7(), name, "moved", commit, previous, at));
      }
    }

    observed.AddRange(before.Where(t => !now.ContainsKey(t.Key)).Select(t => new ObservedTag(Guid.CreateVersion7(), t.Key, "deleted", null, t.Value, at)));
    return observed;
  }

  private void Spool(string batch)
  {
    foreach (var dropped in spool.Write(Guid.CreateVersion7(), batch))
    {
      log.Error($"Git spool full: dropped the oldest waiting batch {dropped}.");
    }
  }
}
