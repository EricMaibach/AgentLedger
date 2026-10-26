using System.Text.Json;
using AgentLedger.Cli.Git;
using AgentLedger.Cli.Spooling;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.Cli.Tests.Git;

// What a hook does for tags (ADR 0021), against a real repository: the existing tags at first sight, then each
// tag created, moved or deleted, checked only when the tag files changed.
public sealed class GitCatchUpTags : IDisposable
{
  private readonly TempGitRepository _repo = new();
  private readonly TempDirectory _data = new();
  private readonly EventSpool _spool;
  private readonly FakeLog _log = new();
  private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow.AddMinutes(-1));

  public GitCatchUpTags() => _spool = new EventSpool(Path.Combine(_data.Path, "spool", "git"), SpoolLimits.Default);

  public void Dispose()
  {
    _repo.Dispose();
    _data.Dispose();
  }

  private Task RunAsync(string? directory = null, IGitRunner? runner = null) =>
    new GitCatchUp(runner ?? new ProcessGitRunner(), new GitRepositoryStateStore(Path.Combine(_data.Path, "git")), _spool, _log, _clock,
      machineName: "devbox").RunAsync(directory ?? _repo.Path);

  // Every tag observation spooled so far, as (name, change, commit, previous commit).
  private List<(string? Name, string? Change, string? Commit, string? Previous)> Observed() =>
    [.. _spool.List().Select(s => JsonDocument.Parse(_spool.Read(s)!).RootElement)
      .SelectMany(b => b.GetProperty("tags").EnumerateArray())
      .Select(t => (t.GetProperty("name").GetString(), t.GetProperty("change").GetString(),
        t.TryGetProperty("commit", out var c) ? c.GetString() : null,
        t.TryGetProperty("previousCommit", out var p) ? p.GetString() : null))];

  [Fact]
  public async Task RecordsTheExistingTagsAtFirstSight()
  {
    var first = _repo.Commit("first");
    _repo.Git("tag", "v0.1.0");                                // lightweight: points at the commit
    var second = _repo.Commit("second");
    _repo.Git("tag", "-a", "v0.2.0", "-m", "Release 0.2.0");   // annotated: a tag object pointing at the commit

    await RunAsync();

    Observed().ShouldBe([("v0.1.0", "existing", first, null), ("v0.2.0", "existing", second, null)], ignoreOrder: true);
  }

  [Fact]
  public async Task RecordsTagsCreatedMovedAndDeleted()
  {
    var first = _repo.Commit("first");
    _repo.Git("tag", "keep");
    _repo.Git("tag", "gone");
    await RunAsync(); // baseline: keep and gone exist

    var second = _repo.Commit("second");
    _repo.Git("tag", "v1.0.0");
    _repo.Git("tag", "-f", "keep");
    _repo.Git("tag", "-d", "gone");
    await RunAsync();

    Observed().Where(o => o.Change != "existing").ShouldBe([
      ("v1.0.0", "created", second, null),
      ("keep", "moved", second, first),
      ("gone", "deleted", null, first)], ignoreOrder: true);
  }

  [Fact]
  public async Task SendsNothingWhenTheTagsDidNotChange()
  {
    _repo.Commit("first");
    _repo.Git("tag", "v1");
    await RunAsync();
    var afterBaseline = _spool.List().Count;

    _repo.Commit("second"); // HEAD moves, tags don't
    await RunAsync();
    _repo.Git("pack-refs", "--all"); // the tag files change, the tags don't
    await RunAsync();

    Observed().Count(o => o.Change != "existing").ShouldBe(0);
    _spool.List().Count(s => JsonDocument.Parse(_spool.Read(s)!).RootElement.GetProperty("tags").GetArrayLength() > 0).ShouldBe(afterBaseline);
  }

  [Fact]
  public async Task WorktreesShareTagsAndReportThemOnce()
  {
    _repo.Commit("first");
    var worktree = Path.Combine(_data.Path, "worktree");
    _repo.Git("worktree", "add", "-q", worktree);
    await RunAsync();
    await RunAsync(worktree);

    _repo.Git("tag", "v1");
    await RunAsync();
    await RunAsync(worktree);

    Observed().Count(o => o.Name == "v1").ShouldBe(1);
  }

  [Fact]
  public async Task SplitsManyTagsIntoBatches()
  {
    var commit = _repo.Commit("first");
    var packed = "# pack-refs with: peeled fully-peeled sorted\n"
      + string.Concat(Enumerable.Range(0, GitCatchUp.MaxTagsPerBatch + 1).Select(i => $"{commit} refs/tags/t{i:D5}\n"));
    File.WriteAllText(Path.Combine(_repo.Path, ".git", "packed-refs"), packed);

    await RunAsync();

    Observed().Count.ShouldBe(GitCatchUp.MaxTagsPerBatch + 1);
    _spool.List().Count.ShouldBe(2);
  }

  [Fact]
  public async Task LogsWhenGitFailsAndTriesAgainNextTime()
  {
    _repo.Commit("first");
    _repo.Git("tag", "v1");

    await RunAsync(runner: new FailingGitRunner());
    Observed().ShouldBeEmpty();
    _log.Lines.ShouldContain(l => l.StartsWith("ERROR") && l.Contains("tags"));

    await RunAsync();
    Observed().ShouldContain(o => o.Name == "v1");
  }

  private sealed class FailingGitRunner : IGitRunner
  {
    public Task<GitRunResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, TimeSpan timeout) =>
      Task.FromResult(new GitRunResult(ProcessGitRunner.NotRun, ""));
  }
}
