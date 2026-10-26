using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.GitAggregate;
using AgentLedger.UseCases;
using AgentLedger.UseCases.Git;
using AgentLedger.UseCases.Git.IngestGitObservations;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Git;

// A repository's newly observed commits (ADR 0021).
public sealed class IngestGitObservationsHandlerHandle
{
  private static readonly DateTimeOffset Now = new(2026, 10, 26, 2, 0, 0, TimeSpan.Zero);
  private static readonly Guid KeyId = Guid.CreateVersion7();
  private const string HashA = "3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba";
  private const string HashB = "03352b0d8d9391e5508b8f1cc994c2ff99686886";

  private readonly IGitObservationStore _store = Substitute.For<IGitObservationStore>();
  private readonly IngestGitObservationsHandler _handler;
  private IReadOnlyList<GitCommit>? _stored;
  private IReadOnlyList<GitTagObservation>? _storedTags;

  public IngestGitObservationsHandlerHandle()
  {
    StoreReportsNew(int.MaxValue);
    _handler = new IngestGitObservationsHandler(_store, new FakeTimeProvider(Now));
  }

  private void StoreReportsNew(int count) =>
    _store.AddAsync(Arg.Do<IReadOnlyList<GitCommit>>(c => _stored = c), Arg.Do<IReadOnlyList<GitTagObservation>>(t => _storedTags = t),
        Arg.Any<CancellationToken>())
      .Returns(call => Math.Min(count, call.Arg<IReadOnlyList<GitCommit>>().Count));

  private static GitCommitInput Commit(string hash = HashA, string[]? parents = null, IReadOnlyList<GitFileInput>? files = null) =>
    new(hash, parents ?? [HashB], Now.AddHours(-1), Now.AddMinutes(-59), "Add login\n",
      files ?? [new GitFileInput("src/Login.cs", "added", null)], FilesTruncated: false);

  private ValueTask<Result<GitObservationsStored>> Handle(string repository = "github.com/owner/repo", params GitCommitInput[] commits) =>
    _handler.Handle(new IngestGitObservationsCommand(repository, commits.Length == 0 ? [Commit()] : commits, [], KeyId), CancellationToken.None);

  private ValueTask<Result<GitObservationsStored>> HandleTags(params GitTagInput[] tags) =>
    _handler.Handle(new IngestGitObservationsCommand("github.com/owner/repo", [], tags, KeyId), CancellationToken.None);

  private static GitTagInput Tag(string change = "created", string? commit = HashA, string? previous = null, string name = "v1.0.0") =>
    new(name, change, commit, previous, Now.AddMinutes(-1));

  [Fact]
  public void HasTheAgreedBatchLimit()
  {
    Constants.MAX_GIT_COMMITS_PER_BATCH.ShouldBe(200);
  }

  [Fact]
  public async Task StoresTheCommitsStampedWithArrivalAndKey()
  {
    var result = await Handle(commits: Commit(files: [new GitFileInput("src/New.cs", "Renamed", "src/Old.cs")]));

    result.IsSuccess.ShouldBeTrue();
    var commit = _stored.ShouldNotBeNull().ShouldHaveSingleItem();
    commit.Repository.ShouldBe("github.com/owner/repo");
    commit.Hash.ShouldBe(CommitHash.From(HashA));
    commit.ParentHashes.ShouldBe([CommitHash.From(HashB)]);
    commit.Message.ShouldBe("Add login\n");
    commit.Files.ShouldBe([new GitCommitFile("src/New.cs", GitFileChange.Renamed, "src/Old.cs")]);
    commit.ApiKeyId.ShouldBe(ApiKeyId.From(KeyId));
    commit.ReceivedAt.ShouldBe(Now);
  }

  [Fact]
  public async Task SaysHowManyWereNewAndHowManyAlreadyStored()
  {
    StoreReportsNew(1);

    (await Handle(commits: [Commit(HashA), Commit(HashB, parents: [])])).Value.ShouldBe(new GitObservationsStored(1, 1, 0));
  }

  [Theory]
  [InlineData(" ")]
  [InlineData("")]
  public async Task RequiresARepository(string repository)
  {
    (await Handle(repository)).ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("repository");
  }

  [Fact]
  public async Task RejectsAnEmptyBatchOrTooManyCommits()
  {
    var empty = await _handler.Handle(new IngestGitObservationsCommand("github.com/owner/repo", [], [], KeyId), CancellationToken.None);
    var tooMany = Enumerable.Range(0, Constants.MAX_GIT_COMMITS_PER_BATCH + 1)
      .Select(i => Commit(i.ToString("x40", System.Globalization.CultureInfo.InvariantCulture), parents: [])).ToArray();

    empty.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("commits");
    (await Handle(commits: tooMany)).ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("commits");
  }

  [Theory]
  [InlineData("not-a-hash", HashB, "added")]   // the commit's hash
  [InlineData(HashA, "abc", "added")]          // a parent
  [InlineData(HashA, HashB, "exploded")]       // a file change
  public async Task RejectsAnInvalidCommit(string hash, string parent, string change)
  {
    var result = await Handle(commits: Commit(hash, [parent], [new GitFileInput("src/x.cs", change, null)]));

    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("commits");
    _stored.ShouldBeNull();
  }

  [Fact]
  public async Task RejectsTheSameCommitTwiceInOneBatch()
  {
    (await Handle(commits: [Commit(), Commit()])).ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("commits");
  }

  [Fact]
  public async Task RejectsMoreFilesThanAreListed()
  {
    var files = Enumerable.Range(0, GitCommit.MaxFiles + 1).Select(i => new GitFileInput($"f{i}", "modified", null)).ToList();

    (await Handle(commits: Commit(files: files))).ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("commits");
  }

  [Fact]
  public async Task RecordsTagObservations()
  {
    var result = await HandleTags(Tag("moved", HashA, HashB, "v0.4.3"), Tag("existing", HashB, null, "v0.4.2"));

    result.Value.ShouldBe(new GitObservationsStored(0, 0, 2));
    var moved = _storedTags.ShouldNotBeNull().First();
    moved.Repository.ShouldBe("github.com/owner/repo");
    moved.TagName.ShouldBe("v0.4.3");
    moved.Change.ShouldBe(GitTagChange.Moved);
    moved.Commit.ShouldBe(CommitHash.From(HashA));
    moved.PreviousCommit.ShouldBe(CommitHash.From(HashB));
    moved.ObservedAt.ShouldBe(Now.AddMinutes(-1));
    moved.ApiKeyId.ShouldBe(ApiKeyId.From(KeyId));
    moved.ReceivedAt.ShouldBe(Now);
  }

  [Theory]
  [InlineData("renamed", HashA, null)]   // not a change the CLI reports
  [InlineData("moved", HashA, null)]     // a move needs the previous commit
  [InlineData("created", "abc", null)]   // not a commit hash
  public async Task RejectsAnInvalidTagObservation(string change, string? commit, string? previous)
  {
    var result = await HandleTags(Tag(change, commit, previous));

    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("tags");
    _storedTags.ShouldBeNull();
  }

  [Fact]
  public async Task RejectsTooManyTags()
  {
    var tags = Enumerable.Range(0, Constants.MAX_GIT_TAGS_PER_BATCH + 1).Select(i => Tag(name: $"t{i}")).ToArray();

    (await HandleTags(tags)).ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("tags");
  }
}
