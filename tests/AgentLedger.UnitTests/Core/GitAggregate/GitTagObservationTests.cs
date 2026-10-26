using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.GitAggregate;

namespace AgentLedger.UnitTests.Core.GitAggregate;

// A tag seen created, moved, deleted, or already there at the baseline (ADR 0021): appended, never updated.
public sealed class GitTagObservationConstructor
{
  private static readonly DateTimeOffset Seen = new(2026, 10, 26, 3, 0, 0, TimeSpan.Zero);
  private static readonly ApiKeyId Key = ApiKeyId.From(Guid.CreateVersion7());
  private static readonly CommitHash A = CommitHash.From("3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba");
  private static readonly CommitHash B = CommitHash.From("03352b0d8d9391e5508b8f1cc994c2ff99686886");

  private static GitTagObservation Observation(GitTagChange change, CommitHash? commit, CommitHash? previous, string name = "v0.4.3",
    string repository = "github.com/owner/repo") =>
    new(GitTagObservationId.From(Guid.CreateVersion7()), repository, name, change, commit, previous, Seen, Key, Seen.AddSeconds(5));

  [Fact]
  public void StoresEveryValue()
  {
    var id = GitTagObservationId.From(Guid.CreateVersion7());

    var observation = new GitTagObservation(id, "github.com/owner/repo", "v0.4.3", GitTagChange.Moved, A, B, Seen, Key, Seen.AddSeconds(5));

    observation.Id.ShouldBe(id);
    observation.Repository.ShouldBe("github.com/owner/repo");
    observation.TagName.ShouldBe("v0.4.3");
    observation.Change.ShouldBe(GitTagChange.Moved);
    observation.Commit.ShouldBe(A);
    observation.PreviousCommit.ShouldBe(B);
    observation.ObservedAt.ShouldBe(Seen);
    observation.ApiKeyId.ShouldBe(Key);
    observation.ReceivedAt.ShouldBe(Seen.AddSeconds(5));
  }

  [Fact]
  public void AcceptsEachChangeWithItsCommits()
  {
    Observation(GitTagChange.Existing, A, null).Commit.ShouldBe(A);
    Observation(GitTagChange.Created, A, null).Commit.ShouldBe(A);
    Observation(GitTagChange.Moved, A, B).PreviousCommit.ShouldBe(B);
    Observation(GitTagChange.Deleted, null, B).PreviousCommit.ShouldBe(B);
  }

  [Theory]
  [InlineData("Existing", false, false)] // needs its commit
  [InlineData("Created", true, true)]    // has no previous commit
  [InlineData("Moved", true, false)]     // needs the previous commit
  [InlineData("Deleted", true, true)]    // points at nothing any more
  public void RejectsCommitsThatDoNotFitTheChange(string change, bool withCommit, bool withPrevious)
  {
    Assert.ThrowsAny<ArgumentException>(() =>
      Observation(GitTagChange.FromName(change), withCommit ? A : null, withPrevious ? B : null));
  }

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  public void RequiresATagName(string name)
  {
    Assert.ThrowsAny<ArgumentException>(() => Observation(GitTagChange.Created, A, null, name: name));
  }

  [Fact]
  public void RequiresARepository()
  {
    Assert.ThrowsAny<ArgumentException>(() => Observation(GitTagChange.Created, A, null, repository: " "));
  }
}
