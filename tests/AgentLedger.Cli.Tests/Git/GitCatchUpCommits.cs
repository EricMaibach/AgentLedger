using System.Text.Json;
using AgentLedger.Cli.Git;
using AgentLedger.Cli.Spooling;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.Cli.Tests.Git;

// What a hook does for commits (ADR 0021), against a real repository: a baseline on first sight, then the
// commits made since, oldest first, into the spool.
public sealed class GitCommitCatchUpRun : IDisposable
{
  private readonly TempGitRepository _repo = new();
  private readonly TempDirectory _data = new();
  private readonly EventSpool _spool;
  private readonly FakeLog _log = new();
  private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow.AddMinutes(-1));

  public GitCommitCatchUpRun() => _spool = new EventSpool(Path.Combine(_data.Path, "spool", "git"), SpoolLimits.Default);

  public void Dispose()
  {
    _repo.Dispose();
    _data.Dispose();
  }

  private Task RunAsync(int maxCommits = 200, IGitRunner? runner = null) =>
    new GitCommitCatchUp(runner ?? new ProcessGitRunner(), new GitRepositoryStateStore(Path.Combine(_data.Path, "git")), _spool, _log, _clock,
      machineName: "devbox", maxCommits).RunAsync(_repo.Path);

  // The spooled batches' commits, in file order of the batches (spool names are time-ordered to the millisecond).
  private List<JsonElement> SpooledCommits() =>
    [.. _spool.List().Select(s => JsonDocument.Parse(_spool.Read(s)!).RootElement)
      .SelectMany(b => b.GetProperty("commits").EnumerateArray())
      .OrderBy(c => c.GetProperty("committedAt").GetDateTimeOffset())];

  [Fact]
  public async Task OnlyRecordsABaselineTheFirstTime()
  {
    _repo.Commit("before the CLI");

    await RunAsync();

    _spool.List().ShouldBeEmpty(); // history from before the CLI isn't sent
  }

  [Fact]
  public async Task SendsCommitsMadeSinceAndRemembersThem()
  {
    _repo.Commit("before the CLI");
    await RunAsync();

    var first = _repo.Commit("Add login\n\nDetails.", "login.cs", date: DateTimeOffset.UtcNow);
    var second = _repo.Commit("Fix login", "login.cs", date: DateTimeOffset.UtcNow.AddSeconds(1));
    await RunAsync();
    await RunAsync(); // nothing new

    var commits = SpooledCommits();
    commits.Select(c => c.GetProperty("hash").GetString()).ShouldBe([first, second]);
    commits[0].GetProperty("message").GetString().ShouldBe("Add login\n\nDetails.\n");
    commits[0].GetProperty("files")[0].GetProperty("path").GetString().ShouldBe("login.cs");
    commits[0].GetProperty("files")[0].GetProperty("change").GetString().ShouldBe("added");
    commits[1].GetProperty("parents")[0].GetString().ShouldBe(first);
  }

  [Fact]
  public async Task NamesTheRepositoryByItsRemoteOrElseByHostAndFolder()
  {
    _repo.Commit("before");
    await RunAsync();
    _repo.Commit("local", date: DateTimeOffset.UtcNow);
    await RunAsync();
    _repo.Git("remote", "add", "origin", "git@github.com:owner/repo.git");
    _repo.Commit("with a remote", date: DateTimeOffset.UtcNow.AddSeconds(1));
    await RunAsync();

    var repositories = _spool.List().Select(s => JsonDocument.Parse(_spool.Read(s)!).RootElement.GetProperty("repository").GetString()).ToList();
    repositories.ShouldContain($"devbox:{_repo.Path}");
    repositories.ShouldContain("github.com/owner/repo");
  }

  [Fact]
  public async Task SendsALongRunInSeveralBatchesOldestFirst()
  {
    _repo.Commit("before");
    await RunAsync(maxCommits: 2);
    var hashes = Enumerable.Range(0, 5).Select(i => _repo.Commit($"c{i}", date: DateTimeOffset.UtcNow.AddSeconds(i))).ToList();

    await RunAsync(maxCommits: 2);
    SpooledCommits().Count.ShouldBe(2); // the oldest two, not the newest
    await RunAsync(maxCommits: 2);
    await RunAsync(maxCommits: 2);

    SpooledCommits().Select(c => c.GetProperty("hash").GetString()).ShouldBe(hashes);
  }

  [Fact]
  public async Task DoesNotSendHistoryFromBeforeTheBaseline()
  {
    // Switching to an old branch moves HEAD onto commits made long before the CLI saw the repository.
    _repo.Commit("start");
    _repo.Git("checkout", "-q", "-b", "old");
    _repo.Commit("old work", "old.txt", date: DateTimeOffset.UtcNow.AddYears(-1));
    _repo.Git("checkout", "-q", "main");
    await RunAsync();

    _repo.Git("checkout", "-q", "old");
    await RunAsync();

    _spool.List().ShouldBeEmpty();
  }

  [Fact]
  public async Task LogsWhenGitFailsAndCarriesOn()
  {
    _repo.Commit("before");
    await RunAsync();
    _repo.Commit("after", date: DateTimeOffset.UtcNow);

    await RunAsync(runner: new FailingGitRunner());

    _spool.List().ShouldBeEmpty();
    _log.Lines.ShouldContain(l => l.StartsWith("ERROR") && l.Contains("git"));
  }

  [Fact]
  public async Task DoesNothingOutsideARepository()
  {
    await new GitCommitCatchUp(new ProcessGitRunner(), new GitRepositoryStateStore(Path.Combine(_data.Path, "git")), _spool, _log, _clock,
      "devbox", 200).RunAsync(_data.Path);

    _spool.List().ShouldBeEmpty();
  }

  private sealed class FailingGitRunner : IGitRunner
  {
    public Task<GitRunResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, TimeSpan timeout) =>
      Task.FromResult(new GitRunResult(ProcessGitRunner.NotRun, ""));
  }
}
