using AgentLedger.Cli.Envelope;

namespace AgentLedger.Cli.Tests.Envelope;

public sealed class GitContextReaderNormalizeRemote
{
  [Theory]
  [InlineData("git@github.com:owner/repo.git", "github.com/owner/repo")]
  [InlineData("https://github.com/owner/repo.git", "github.com/owner/repo")]
  [InlineData("https://github.com/owner/repo", "github.com/owner/repo")]
  [InlineData("ssh://git@github.com:22/owner/repo.git", "github.com/owner/repo")]
  [InlineData("https://gitlab.example.com/group/subgroup/repo.git", "gitlab.example.com/group/subgroup/repo")]
  public void ReducesRemotesToHostAndPath(string remote, string expected)
  {
    GitContextReader.NormalizeRemote(remote).ShouldBe(expected);
  }

  [Theory]
  [InlineData("https://ghp_secretToken123@github.com/owner/repo.git")]
  [InlineData("https://user:ghp_secretToken123@github.com/owner/repo.git")]
  public void StripsCredentials(string remote)
  {
    var normalized = GitContextReader.NormalizeRemote(remote);

    normalized.ShouldBe("github.com/owner/repo");
    normalized.ShouldNotContain("ghp_secretToken123");
  }
}
