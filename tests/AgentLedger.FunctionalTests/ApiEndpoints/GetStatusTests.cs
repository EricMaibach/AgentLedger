using System.Reflection;
using AgentLedger.Web.Status;

namespace AgentLedger.FunctionalTests.ApiEndpoints;

public sealed class GetStatusTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  [Fact]
  public async Task ReturnsServiceName()
  {
    var result = await factory.CreateClient().GetAndDeserializeAsync<StatusResponse>("/status");

    result.Service.ShouldBe("AgentLedger");
  }

  [Fact]
  public async Task ReportsTheReleaseVersionAndCommit()
  {
    // The informational version carries the release version and commit (e.g. "0.2.0+3f7a9c1…"),
    // which the release workflow stamps into the image; the assembly version (1.0.0.0) carries neither.
    var expected = typeof(GetStatus).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    var result = await factory.CreateClient().GetAndDeserializeAsync<StatusResponse>("/status");

    result.Version.ShouldBe(expected);
  }
}
