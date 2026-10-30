using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AgentLedger.Core.UserAggregate;
using AgentLedger.Core.UserAggregate.Specifications;
using AgentLedger.FunctionalTests.ApiEndpoints;
using AgentLedger.FunctionalTests.Authentication;
using AgentLedger.UseCases.Enrollment.CreateEnrollmentCode;
using Ardalis.SharedKernel;
using Mediator;

namespace AgentLedger.FunctionalTests.Enrollment;

// POST /enroll (ADR 0024): a machine with no key yet exchanges a one-time code for its own ingest key.
public sealed class EnrollTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private async Task<string> CreateCodeAsync()
  {
    // Directly through the use case: the machines page has its own tests.
    await factory.EnsureUserAsync("tester");
    using var scope = factory.Services.CreateScope();
    var user = await scope.ServiceProvider.GetRequiredService<IReadRepository<User>>()
      .FirstOrDefaultAsync(new UserByUsernameSpec(Username.From("tester")));
    var created = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CreateEnrollmentCodeCommand(user!.Id.Value));
    return created.Value.Code;
  }

  private Task<HttpResponseMessage> EnrollAsync(string code, string host = "devbox") =>
    factory.CreateClient().PostAsJsonAsync("/enroll", new { code, host });

  [Fact]
  public async Task GivesTheMachineAnIngestKeyThatWorks()
  {
    var response = await EnrollAsync(await CreateCodeAsync(), "agentledger-devcontainer");

    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    var key = json.GetProperty("apiKey").GetString()!;
    key.ShouldStartWith("agl_ingest_");
    json.GetProperty("keyName").GetString().ShouldStartWith("agentledger-devcontainer (connected ");

    var ingest = factory.CreateClientWithKey(key);
    var sent = await ingest.PostAsync("/events", new StringContent(TestEnvelope.Json(), Encoding.UTF8, "application/json"));
    sent.StatusCode.ShouldBe(HttpStatusCode.Created);
  }

  [Fact]
  public async Task WorksOnlyOnce()
  {
    var code = await CreateCodeAsync();
    (await EnrollAsync(code)).StatusCode.ShouldBe(HttpStatusCode.OK);

    var again = await EnrollAsync(code, "another");

    again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await again.Content.ReadAsStringAsync()).ShouldContain("isn't valid");
  }

  [Fact]
  public async Task RefusesAnUnknownCode()
  {
    (await EnrollAsync("AAAA-AAAA")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task NeedsNoSignInOrKey()
  {
    // A new machine has neither: the code is the credential. An anonymous client gets a 400, not a 401 or a redirect.
    var response = await factory.CreateBrowser().PostAsJsonAsync("/enroll", new { code = "AAAA-AAAA", host = "devbox" });

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
