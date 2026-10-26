using AgentLedger.Cli.Envelope;

namespace AgentLedger.Cli.Tests.Envelope;

// The host name used everywhere the CLI names this machine: events, and repositories without a remote (ADR 0021).
public sealed class HostNameFor
{
  [Fact]
  public void IsTheMachineName()
  {
    HostName.For(new FakeHostEnvironment { MachineName = "a71c73ee5e92" }).ShouldBe("a71c73ee5e92");
  }

  [Fact]
  public void IgnoresABlankSetting()
  {
    var environment = new FakeHostEnvironment { MachineName = "a71c73ee5e92" };
    environment.Variables["AGENTLEDGER_HOST"] = "  ";

    HostName.For(environment).ShouldBe("a71c73ee5e92");
  }

  [Fact]
  public void CanBeSetForContainersWithRandomNames()
  {
    var environment = new FakeHostEnvironment { MachineName = "a71c73ee5e92" };
    environment.Variables["AGENTLEDGER_HOST"] = "agentledger-devcontainer";

    HostName.For(environment).ShouldBe("agentledger-devcontainer");
  }
}
