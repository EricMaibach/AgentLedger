using AgentLedger.Cli.Git;

namespace AgentLedger.Cli.Tests.Git;

// Runs git as a process: only when HEAD moved (ADR 0021), so cost matters less than never hanging or throwing.
public sealed class ProcessGitRunnerRun : IDisposable
{
  private readonly TempGitRepository _repo = new();

  public void Dispose() => _repo.Dispose();

  [Fact]
  public async Task ReturnsGitsOutput()
  {
    var head = _repo.Commit("first");

    var result = await new ProcessGitRunner().RunAsync(_repo.Path, ["rev-parse", "HEAD"], TimeSpan.FromSeconds(5));

    result.ExitCode.ShouldBe(0);
    result.Output.Trim().ShouldBe(head);
  }

  [Fact]
  public async Task ReportsAFailureInsteadOfThrowing()
  {
    var result = await new ProcessGitRunner().RunAsync(_repo.Path, ["rev-parse", "no-such-revision"], TimeSpan.FromSeconds(5));

    result.ExitCode.ShouldNotBe(0);
  }

  [Fact]
  public async Task ReportsAMissingGitInsteadOfThrowing()
  {
    var result = await new ProcessGitRunner("git-that-does-not-exist").RunAsync(_repo.Path, ["--version"], TimeSpan.FromSeconds(5));

    result.ExitCode.ShouldBe(ProcessGitRunner.NotRun);
  }
}
