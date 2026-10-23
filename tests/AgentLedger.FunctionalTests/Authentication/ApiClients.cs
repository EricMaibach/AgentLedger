using System.Net.Http.Headers;
using AgentLedger.Core.UserAggregate;
using AgentLedger.Core.UserAggregate.Specifications;
using AgentLedger.UseCases.ApiKeys.CreateApiKey;
using Ardalis.SharedKernel;
using Mediator;

namespace AgentLedger.FunctionalTests.Authentication;

// Clients that authenticate with an API key, as the CLI and scripts do (ADR 0019).
internal static class ApiClients
{
  // Creates a key directly through the use case (the page has its own tests) and returns its token.
  public static async Task<string> CreateApiKeyAsync(this CustomWebApplicationFactory<Program> factory, string scope = "ingest",
    string username = "tester", string name = "test key")
  {
    await factory.EnsureUserAsync(username);
    using var services = factory.Services.CreateScope();
    var user = await services.ServiceProvider.GetRequiredService<IReadRepository<User>>()
      .FirstOrDefaultAsync(new UserByUsernameSpec(Username.From(username)));
    var result = await services.ServiceProvider.GetRequiredService<IMediator>()
      .Send(new CreateApiKeyCommand(user!.Id.Value, name, scope, ExpiresInDays: null));
    return result.Value.Token;
  }

  public static HttpClient CreateClientWithKey(this CustomWebApplicationFactory<Program> factory, string? token)
  {
    var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    if (token is not null)
    {
      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    return client;
  }

  // What the CLI is: a client with an ingest key.
  public static async Task<HttpClient> CreateIngestClientAsync(this CustomWebApplicationFactory<Program> factory, string username = "tester") =>
    factory.CreateClientWithKey(await factory.CreateApiKeyAsync("ingest", username));
}
