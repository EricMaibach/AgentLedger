using AgentLedger.Cli.Envelope;

namespace AgentLedger.Cli.Tests.Envelope;

public sealed class GitContextReaderRead : IDisposable
{
  private readonly TempDirectory _dir = new();

  public void Dispose() => _dir.Dispose();

  // A minimal repository as git lays it out on disk: .git/HEAD and .git/config.
  private string CreateRepo(string relativePath = "repo", string head = "ref: refs/heads/main\n", string? originUrl = "git@github.com:owner/repo.git")
  {
    _dir.Write($"{relativePath}/.git/HEAD", head);
    _dir.Write($"{relativePath}/.git/config", originUrl is null
      ? "[core]\n\tbare = false\n"
      : $"[core]\n\tbare = false\n[remote \"origin\"]\n\turl = {originUrl}\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n");
    return System.IO.Path.Combine(_dir.Path, relativePath);
  }

  [Fact]
  public void ReturnsNothingOutsideARepository()
  {
    var context = GitContextReader.Read(_dir.CreateDirectory("not-a-repo"));

    context.ShouldBe(GitContext.None);
  }

  [Fact]
  public void ReadsBranchAndNormalizedOriginFromTheRepositoryRoot()
  {
    var repo = CreateRepo();

    var context = GitContextReader.Read(repo);

    context.Branch.ShouldBe("main");
    context.Repo.ShouldBe("github.com/owner/repo");
    context.Worktree.ShouldBeNull(); // the main working tree, not a linked worktree
  }

  [Fact]
  public void FindsTheRepositoryFromASubdirectory()
  {
    var repo = CreateRepo();
    var nested = Directory.CreateDirectory(System.IO.Path.Combine(repo, "src", "deep")).FullName;

    GitContextReader.Read(nested).Branch.ShouldBe("main");
  }

  [Fact]
  public void ReadsBranchNamesContainingSlashes()
  {
    var repo = CreateRepo(head: "ref: refs/heads/feature/story-123\n");

    GitContextReader.Read(repo).Branch.ShouldBe("feature/story-123");
  }

  [Fact]
  public void LeavesBranchEmptyWhenHeadIsDetached()
  {
    var repo = CreateRepo(head: "3f786850e387550fdab836ed7e6dc881de23001b\n");

    GitContextReader.Read(repo).Branch.ShouldBeNull();
  }

  [Fact]
  public void LeavesRepoEmptyWithoutAnOriginRemote()
  {
    var repo = CreateRepo(originUrl: null);

    GitContextReader.Read(repo).Repo.ShouldBeNull();
  }

  [Fact]
  public void ReadsALinkedWorktree()
  {
    // `git worktree add ../story-123` creates a folder whose .git is a *file* pointing into the
    // main repository's .git/worktrees/<name>, which has its own HEAD and a commondir back to .git.
    var main = CreateRepo("main");
    _dir.Write("main/.git/worktrees/story-123/HEAD", "ref: refs/heads/story-123\n");
    _dir.Write("main/.git/worktrees/story-123/commondir", "../..\n");
    _dir.Write("story-123/.git", $"gitdir: {System.IO.Path.Combine(main, ".git", "worktrees", "story-123")}\n");
    var worktree = System.IO.Path.Combine(_dir.Path, "story-123");

    var context = GitContextReader.Read(worktree);

    context.Branch.ShouldBe("story-123");                // from the worktree's own HEAD
    context.Repo.ShouldBe("github.com/owner/repo");      // from the shared config
    context.Worktree.ShouldBe(worktree);
  }

  private const string CommitA = "2376609a3f019662cf2f5324c09cdb4b61e3be1e";
  private const string CommitB = "4c49c1ea57c878d3a185d98340c599d6afffe9bd";

  [Fact]
  public void ReadsTheCommitTheBranchPointsAt()
  {
    // HEAD names the branch; the branch's own file holds the commit (ADR 0021).
    var repo = CreateRepo();
    _dir.Write("repo/.git/refs/heads/main", CommitA + "\n");

    GitContextReader.Read(repo).Commit.ShouldBe(CommitA);
  }

  [Fact]
  public void ReadsAPackedBranch()
  {
    // After `git gc`, branches live in packed-refs instead of their own files.
    var repo = CreateRepo();
    _dir.Write("repo/.git/packed-refs", $"# pack-refs with: peeled fully-peeled sorted\n{CommitB} refs/heads/other\n{CommitA} refs/heads/main\n");

    GitContextReader.Read(repo).Commit.ShouldBe(CommitA);
  }

  [Fact]
  public void PrefersTheBranchFileOverAnOlderPackedEntry()
  {
    // A commit after packing updates the loose file; packed-refs keeps the old value.
    var repo = CreateRepo();
    _dir.Write("repo/.git/packed-refs", $"{CommitB} refs/heads/main\n");
    _dir.Write("repo/.git/refs/heads/main", CommitA + "\n");

    GitContextReader.Read(repo).Commit.ShouldBe(CommitA);
  }

  [Fact]
  public void ReadsTheCommitWhenHeadIsDetached()
  {
    var repo = CreateRepo(head: CommitA + "\n");

    GitContextReader.Read(repo).Commit.ShouldBe(CommitA);
  }

  [Fact]
  public void LeavesTheCommitEmptyOnABranchWithNoCommitsYet()
  {
    GitContextReader.Read(CreateRepo()).Commit.ShouldBeNull();
  }

  [Fact]
  public void ReadsALinkedWorktreesCommitFromTheSharedRefs()
  {
    var main = CreateRepo("main");
    _dir.Write("main/.git/refs/heads/story-123", CommitB + "\n");
    _dir.Write("main/.git/worktrees/story-123/HEAD", "ref: refs/heads/story-123\n");
    _dir.Write("main/.git/worktrees/story-123/commondir", "../..\n");
    _dir.Write("story-123/.git", $"gitdir: {System.IO.Path.Combine(main, ".git", "worktrees", "story-123")}\n");

    GitContextReader.Read(System.IO.Path.Combine(_dir.Path, "story-123")).Commit.ShouldBe(CommitB);
  }

  [Fact]
  public void IgnoresSomethingThatIsNotACommitHash()
  {
    var repo = CreateRepo();
    _dir.Write("repo/.git/refs/heads/main", "not a hash\n");

    GitContextReader.Read(repo).Commit.ShouldBeNull();
  }

  [Fact]
  public void NeverThrowsOnAMalformedRepository()
  {
    // Logging must never break the agent: a broken .git yields no context, not an exception.
    _dir.Write("broken/.git", "not a gitdir line");

    GitContextReader.Read(System.IO.Path.Combine(_dir.Path, "broken")).ShouldBe(GitContext.None);
  }
}
