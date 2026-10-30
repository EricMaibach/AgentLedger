using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

// What the machines page shows (ADR 0024): the install commands, the address choices, and whether a machine's CLI is
// behind the ledger.
public sealed class MachineSetupCommands
{
  [Fact]
  public void InstallsOnLinuxAndMacOsWithTheCode()
  {
    MachineSetup.UnixCommand("http://host.docker.internal:58090/", "K7PX-4QHM")
      .ShouldBe("curl -fsSL http://host.docker.internal:58090/install.sh | sh -s -- --code K7PX-4QHM");
  }

  [Fact]
  public void InstallsOnWindowsWithTheCode()
  {
    MachineSetup.WindowsCommand("http://ledger.lan:58090", "K7PX-4QHM")
      .ShouldBe("$env:AGENTLEDGER_CODE='K7PX-4QHM'; irm http://ledger.lan:58090/install.ps1 | iex");
  }

  [Fact]
  public void OffersToReadTheScriptFirst()
  {
    MachineSetup.ReadFirstCommands("http://ledger.lan:58090", "K7PX-4QHM").ShouldBe([
      "curl -fsSLo install.sh http://ledger.lan:58090/install.sh",
      "less install.sh",
      "sh install.sh --code K7PX-4QHM",
    ]);
  }
}

public sealed class MachineSetupAddresses
{
  [Fact]
  public void OffersTheBrowsingAddressAndTheDevContainerOne()
  {
    MachineSetup.AddressChoices(new Uri("http://172.17.0.1:58090/settings/machines")).ShouldBe([
      ("This address", "http://172.17.0.1:58090"),
      ("From a dev container on this server", "http://host.docker.internal:58090"),
    ]);
  }

  [Fact]
  public void DoesNotRepeatTheDevContainerAddress()
  {
    MachineSetup.AddressChoices(new Uri("http://host.docker.internal:58090/")).ShouldBe([("This address", "http://host.docker.internal:58090")]);
  }

  [Fact]
  public void KeepsTheDefaultPortOut()
  {
    MachineSetup.AddressChoices(new Uri("https://ledger.example.com/settings/machines"))[0].Address.ShouldBe("https://ledger.example.com");
  }

  [Theory]
  [InlineData("http://ledger.lan:58090", "http://ledger.lan:58090")]
  [InlineData(" https://ledger.example.com/ ", "https://ledger.example.com")]
  [InlineData("ftp://ledger.lan", null)]
  [InlineData("ledger.lan:58090", null)]
  [InlineData("http://ledger.lan/sub/path", null)]
  [InlineData("", null)]
  public void AcceptsOnlyAPlainWebAddress(string typed, string? expected)
  {
    MachineSetup.NormalizeAddress(typed).ShouldBe(expected);
  }
}

public sealed class MachineSetupNeedsUpdate
{
  [Theory]
  [InlineData("0.6.0", "0.6.0+9ad4c11", false)]
  [InlineData("0.6.0+abc", "0.6.0+def", false)]
  [InlineData("0.5.2", "0.6.0", true)]
  [InlineData("0.6.0", "0.5.2", true)] // any difference: the CLI follows the ledger, also back
  [InlineData(null, "0.6.0", false)]   // not known yet
  public void ComparesVersionsWithoutTheirBuildPart(string? cli, string ledger, bool needsUpdate)
  {
    MachineSetup.NeedsUpdate(cli, ledger).ShouldBe(needsUpdate);
  }
}

public sealed class MachineSetupRelease
{
  [Theory]
  [InlineData("0.6.0+9ad4c11da934c6a746f0c9bfa8b9798698792837", "0.6.0")]
  [InlineData("0.6.0", "0.6.0")]
  public void LeavesOutTheBuildPart(string version, string release)
  {
    MachineSetup.Release(version).ShouldBe(release);
  }
}
