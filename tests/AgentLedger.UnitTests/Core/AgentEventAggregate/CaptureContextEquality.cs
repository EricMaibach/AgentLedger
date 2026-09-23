using AgentLedger.Core.AgentEventAggregate;

namespace AgentLedger.UnitTests.Core.AgentEventAggregate;

public sealed class CaptureContextEquality
{
  [Fact]
  public void EqualWhenAllValuesMatch()
  {
    var a = new CaptureContext("devbox", "eric", "/workspace", "github.com/EricMaibach/AgentLedger", "main", null);
    var b = new CaptureContext("devbox", "eric", "/workspace", "github.com/EricMaibach/AgentLedger", "main", null);

    a.ShouldBe(b);
  }

  [Fact]
  public void AllowsMissingGitDetails()
  {
    // Agents can run outside a git repository.
    var context = new CaptureContext("devbox", "eric", "/tmp/scratch", null, null, null);

    context.GitRepo.ShouldBeNull();
    context.GitBranch.ShouldBeNull();
    context.GitWorktree.ShouldBeNull();
  }
}
