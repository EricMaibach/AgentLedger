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
}
