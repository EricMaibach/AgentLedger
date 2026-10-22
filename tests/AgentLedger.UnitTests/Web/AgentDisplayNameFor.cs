using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

public sealed class AgentDisplayNameFor
{
  [Theory]
  [InlineData("claude-code", "Claude Code")]
  [InlineData("codex", "Codex")]
  [InlineData("copilot-cli", "Copilot CLI")]
  [InlineData("vs-code-copilot", "VS Code Copilot")]
  [InlineData("cortex-code", "Cortex Code")]
  public void NamesTheSupportedAgents(string wireName, string displayName)
  {
    AgentDisplayName.For(wireName).ShouldBe(displayName);
  }

  [Fact]
  public void ShowsAnUnknownAgentAsItsWireName()
  {
    AgentDisplayName.For("new-agent").ShouldBe("new-agent");
  }
}
