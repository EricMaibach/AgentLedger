using System.Globalization;
using AgentLedger.Cli.Envelope;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Git;

/// <summary>
/// What each hook does for commits (ADR 0021). Reading HEAD costs nothing (it's in .git); only when it moved does
/// git run: the commits since the last HEAD seen, made after the baseline, oldest first, at most
/// <paramref name="maxCommits"/> per run. Spool first, then save the state: a repeat after a crash is harmless.
/// </summary>
internal sealed class GitCommitCatchUp(IGitRunner git, GitRepositoryStateStore states, EventSpool spool, ICliLog log, TimeProvider clock,
  string machineName, int maxCommits = GitCommitCatchUp.DefaultMaxCommits)
{
  public const int DefaultMaxCommits = 200;

  private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(2);

  public async Task RunAsync(string projectDirectory)
  {
    var workingTree = GitContextReader.FindWorkingTreeRoot(projectDirectory);
    var context = GitContextReader.Read(projectDirectory);
    if (workingTree is null || context.Commit is not { } head)
    {
      return; // not a repository, or no commits yet
    }

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

    var range = await git.RunAsync(workingTree,
      ["rev-list", "--reverse", $"--since={state.BaselineAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}", $"{state.Head}..{head}"],
      GitTimeout);
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

    var repository = context.Repo ?? $"{machineName}:{workingTree}";
    foreach (var dropped in spool.Write(Guid.CreateVersion7(), GitBatches.ToJson(repository, GitLogParser.Parse(details.Output))))
    {
      log.Error($"Git spool full: dropped the oldest waiting batch {dropped}.");
    }

    // A long run continues next time from the last commit sent.
    states.Save(state with { Head = hashes.Count < maxCommits ? head : hashes[^1] });
  }
}
