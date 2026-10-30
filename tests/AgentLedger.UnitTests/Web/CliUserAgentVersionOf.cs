using AgentLedger.Web.Users;

namespace AgentLedger.UnitTests.Web;

// The CLI names itself in User-Agent (ADR 0024): agentledger-cli/<version>.
public sealed class CliUserAgentVersionOf
{
  [Theory]
  [InlineData("agentledger-cli/0.6.0", "0.6.0")]
  [InlineData("agentledger-cli/0.6.0+9ad4c11", "0.6.0+9ad4c11")]
  [InlineData("agentledger-cli/0.6.0 (linux-x64)", "0.6.0")]
  [InlineData("curl/8.5.0", null)]
  [InlineData("agentledger-cli/", null)]
  [InlineData("", null)]
  [InlineData(null, null)]
  public void ReadsTheCliVersion(string? userAgent, string? expected)
  {
    CliUserAgent.VersionOf(userAgent).ShouldBe(expected);
  }
}
