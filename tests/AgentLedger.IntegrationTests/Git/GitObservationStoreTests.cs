using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.GitAggregate;
using AgentLedger.Infrastructure.Data;
using AgentLedger.IntegrationTests.Data;
using AgentLedger.UseCases.Git;

namespace AgentLedger.IntegrationTests.Git;

// Storing observed commits (ADR 0021) against real Postgres. Each test uses its own repository name.
[Collection(nameof(PostgresCollection))]
public sealed class GitObservationStoreTests(PostgresFixture db)
{
  private static readonly DateTimeOffset T0 = new(2026, 10, 26, 1, 0, 0, TimeSpan.Zero);

  private readonly string _repository = $"github.com/owner/repo-{Guid.NewGuid():N}";

  [Fact]
  public async Task StoresACommitWithEveryValue()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var commit = Commit(key, "3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba", ["03352b0d8d9391e5508b8f1cc994c2ff99686886"],
      "Add login\n\nWith a body, naïve 🔑.\n",
      [new GitCommitFile("src/Login.cs", GitFileChange.Added, null), new GitCommitFile("docs/new name.md", GitFileChange.Renamed, "docs/old.md")],
      filesTruncated: true);

    (await AddAsync(commit)).ShouldBe(1);

    var loaded = await LoadAsync(commit.Id);
    loaded.Repository.ShouldBe(_repository);
    loaded.Hash.ShouldBe(commit.Hash);
    loaded.ParentHashes.ShouldBe(commit.ParentHashes);
    loaded.AuthoredAt.ShouldBe(T0);
    loaded.CommittedAt.ShouldBe(T0.AddMinutes(1));
    loaded.Message.ShouldBe("Add login\n\nWith a body, naïve 🔑.\n");
    loaded.Files.ShouldBe(commit.Files);
    loaded.FilesTruncated.ShouldBeTrue();
    loaded.ApiKeyId.ShouldBe(key.Id);
    loaded.ReceivedAt.ShouldBe(T0.AddMinutes(2));
  }

  [Fact]
  public async Task StoresAMergeWithTwoParentsAndARootCommitWithNone()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var merge = Commit(key, "1111111111111111111111111111111111111111",
      ["2222222222222222222222222222222222222222", "3333333333333333333333333333333333333333"]);
    var root = Commit(key, "4444444444444444444444444444444444444444", []);

    (await AddAsync(merge, root)).ShouldBe(2);

    (await LoadAsync(merge.Id)).ParentHashes.Count.ShouldBe(2);
    (await LoadAsync(root.Id)).ParentHashes.ShouldBeEmpty();
  }

  [Fact]
  public async Task SkipsCommitsAlreadyStored()
  {
    var key = await ApiKeyPersistence.SaveNewKeyAsync(db);
    var first = Commit(key, "5555555555555555555555555555555555555555", []);
    await AddAsync(first);

    (await AddAsync(first, Commit(key, "6666666666666666666666666666666666666666", []))).ShouldBe(1);
  }

  [Fact]
  public async Task RequiresAKeyThatExists()
  {
    var missing = new ApiKey(ApiKeyId.From(Guid.CreateVersion7()), Core.UserAggregate.UserId.From(Guid.CreateVersion7()), "missing",
      ApiKeyScope.Ingest, ApiKeySecretHash.Of("x"), T0, null);

    await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => AddAsync(Commit(missing, "7777777777777777777777777777777777777777", [])));
  }

  private GitCommit Commit(ApiKey key, string hash, string[] parents, string message = "message\n",
    IReadOnlyList<GitCommitFile>? files = null, bool filesTruncated = false) =>
    new(_repository, CommitHash.From(hash), [.. parents.Select(CommitHash.From)], T0, T0.AddMinutes(1), message,
      files ?? [], filesTruncated, key.Id, T0.AddMinutes(2));

  private async Task<int> AddAsync(params GitCommit[] commits)
  {
    using var scope = db.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<IGitObservationStore>().AddCommitsAsync(commits, CancellationToken.None);
  }

  private async Task<GitCommit> LoadAsync(GitCommitId id)
  {
    using var scope = db.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<GitCommit>().AsNoTracking().SingleAsync(c => c.Id == id);
  }
}
