using AgentLedger.Core.AgentEventReceiptAggregate;
using Ardalis.SmartEnum;

namespace AgentLedger.UnitTests.Core.AgentEventReceiptAggregate;

public sealed class AgentKindFromName
{
  [Theory]
  [InlineData("ClaudeCode")]
  [InlineData("Codex")]
  [InlineData("CopilotCli")]
  [InlineData("VsCodeCopilot")]
  [InlineData("CortexCode")]
  public void FindsEachSupportedAgent(string name)
  {
    AgentKind.FromName(name).Name.ShouldBe(name);
  }

  [Fact]
  public void SupportsExactlyFiveAgents()
  {
    AgentKind.List.Count.ShouldBe(5);
  }

  [Fact]
  public void RejectsUnknownAgent()
  {
    Should.Throw<SmartEnumNotFoundException>(() => AgentKind.FromName("Cursor"));
  }
}
