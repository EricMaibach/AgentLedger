using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.GitAggregate;
using Vogen;

namespace AgentLedger.UnitTests.Core.GitAggregate;

public sealed class CommitHashFrom
{
  [Theory]
  [InlineData("3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba")]
  [InlineData("3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba3e2d081f2042d75a1ef93e36")]
  public void AcceptsAFullSha1OrSha256Hash(string hash)
  {
    CommitHash.From(hash).Value.ShouldBe(hash);
  }

  [Theory]
  [InlineData("")]
  [InlineData("3e2d081")]
  [InlineData("3E2D081F2042D75A1EF93E3611CDBB7F2FCCDBBA")]
  [InlineData("main")]
  public void RejectsAnythingElse(string hash)
  {
    Should.Throw<ValueObjectValidationException>(() => CommitHash.From(hash));
  }
}

public sealed class GitCommitIdFor
{
  [Fact]
  public void NeverChanges()
  {
    // Computed independently (Python), as for transcript IDs: stored IDs depend on this algorithm.
    GitCommitId.For("github.com/EricMaibach/AgentLedger", CommitHash.From("3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba"))
      .Value.ShouldBe(Guid.Parse("2063fde5-2499-8116-9cd7-959482428922"));
  }

  [Fact]
  public void DiffersPerRepository()
  {
    // The same commit can be in two repositories (a fork); each is recorded for its own.
    var hash = CommitHash.From("3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba");

    GitCommitId.For("github.com/a/repo", hash).ShouldNotBe(GitCommitId.For("github.com/b/repo", hash));
  }
}

public sealed class GitCommitConstructor
{
  private static readonly DateTimeOffset Authored = new(2026, 10, 26, 1, 0, 0, TimeSpan.Zero);
  private static readonly ApiKeyId Key = ApiKeyId.From(Guid.CreateVersion7());
  private static readonly CommitHash Hash = CommitHash.From("3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba");
  private static readonly CommitHash Parent = CommitHash.From("03352b0d8d9391e5508b8f1cc994c2ff99686886");

  private static GitCommit Commit(string repository = "github.com/owner/repo", IReadOnlyList<GitCommitFile>? files = null,
    bool filesTruncated = false, string message = "Add login\n\nDetails.\n") =>
    new(repository, Hash, [Parent], Authored, Authored.AddMinutes(1), message,
      files ?? [new GitCommitFile("src/Login.cs", GitFileChange.Added, null)], filesTruncated, Key, Authored.AddMinutes(2));

  [Fact]
  public void StoresEveryValueAndComputesItsId()
  {
    var commit = Commit();

    commit.Id.ShouldBe(GitCommitId.For("github.com/owner/repo", Hash));
    commit.Repository.ShouldBe("github.com/owner/repo");
    commit.Hash.ShouldBe(Hash);
    commit.ParentHashes.ShouldBe([Parent]);
    commit.AuthoredAt.ShouldBe(Authored);
    commit.CommittedAt.ShouldBe(Authored.AddMinutes(1));
    commit.Message.ShouldBe("Add login\n\nDetails.\n");
    commit.Files.ShouldBe([new GitCommitFile("src/Login.cs", GitFileChange.Added, null)]);
    commit.FilesTruncated.ShouldBeFalse();
    commit.ApiKeyId.ShouldBe(Key);
    commit.ReceivedAt.ShouldBe(Authored.AddMinutes(2));
  }

  [Fact]
  public void KeepsTheMessageExactly()
  {
    Commit(message: "").Message.ShouldBe("");
  }

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  public void RequiresARepository(string repository)
  {
    Assert.ThrowsAny<ArgumentException>(() => Commit(repository: repository));
  }

  [Fact]
  public void LimitsTheFilesListed()
  {
    var files = Enumerable.Range(0, GitCommit.MaxFiles + 1).Select(i => new GitCommitFile($"f{i}", GitFileChange.Modified, null)).ToList();

    Assert.ThrowsAny<ArgumentException>(() => Commit(files: files, filesTruncated: true));
  }

  [Fact]
  public void RecordsARenameWithItsPreviousPath()
  {
    var rename = new GitCommitFile("src/New.cs", GitFileChange.Renamed, "src/Old.cs");

    Commit(files: [rename]).Files.ShouldHaveSingleItem().PreviousPath.ShouldBe("src/Old.cs");
  }

  [Fact]
  public void RequiresAPathForEveryFile()
  {
    Assert.ThrowsAny<ArgumentException>(() => new GitCommitFile(" ", GitFileChange.Modified, null));
  }
}
