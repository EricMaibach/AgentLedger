using System.Net;
using System.Text;
using System.Text.Json;
using AgentLedger.FunctionalTests.ApiEndpoints;

namespace AgentLedger.FunctionalTests.Authentication;

// API keys over HTTP (ADR 0019): ingest always needs an ingest key, /api takes a read key or a signed-in
// browser, pages never take keys.
public sealed class ApiKeyAccessTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private static StringContent Envelope(Guid? eventId = null) =>
    new(TestEnvelope.Json(eventId: eventId), Encoding.UTF8, "application/json");

  [Fact]
  public async Task IngestNeedsAKey()
  {
    (await factory.CreateClientWithKey(null).PostAsync("/events", Envelope())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Theory]
  [InlineData("not-a-key")]
  [InlineData("agl_ingest_0000000000000000000001_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // well-formed, unknown
  public async Task IngestRejectsAnUnknownKey(string token)
  {
    (await factory.CreateClientWithKey(token).PostAsync("/events", Envelope())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task IngestRejectsAReadKey()
  {
    var readKey = await factory.CreateApiKeyAsync("read");

    (await factory.CreateClientWithKey(readKey).PostAsync("/events", Envelope())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task IngestRecordsWhoSentTheEvent()
  {
    var ingest = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("ingest", "sender", "dev container"));
    var eventId = Guid.CreateVersion7();

    (await ingest.PostAsync("/events", Envelope(eventId))).StatusCode.ShouldBe(HttpStatusCode.Created);

    var reader = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read"));
    var json = JsonDocument.Parse(await reader.GetStringAsync($"/api/raw/events/{eventId}")).RootElement;
    var sentWith = json.GetProperty("receipts")[0].GetProperty("sentWith");
    sentWith.GetProperty("owner").GetString().ShouldBe("sender");
    sentWith.GetProperty("keyName").GetString().ShouldBe("dev container");
  }

  [Fact]
  public async Task TheReadApiTakesAReadKeyButNotAnIngestKey()
  {
    var readKey = await factory.CreateApiKeyAsync("read");
    var ingestKey = await factory.CreateApiKeyAsync("ingest");

    (await factory.CreateClientWithKey(readKey).GetAsync("/api/raw/events")).StatusCode.ShouldBe(HttpStatusCode.OK);
    (await factory.CreateClientWithKey(ingestKey).GetAsync("/api/raw/events")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    (await factory.CreateClientWithKey("not-a-key").GetAsync("/api/raw/events")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PagesNeverTakeKeys()
  {
    var readKey = await factory.CreateApiKeyAsync("read");

    var response = await factory.CreateClientWithKey(readKey).GetAsync("/raw/events");

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.RedirectTarget().ShouldStartWith("/login");
  }

  [Theory]
  [InlineData("ingest")]
  [InlineData("read")]
  public async Task TellsAKeysHolderWhatTheKeyIs(string scope)
  {
    var key = await factory.CreateApiKeyAsync(scope, "holder", "laptop");

    var response = await factory.CreateClientWithKey(key).GetAsync("/api/keys/current");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    json.GetProperty("name").GetString().ShouldBe("laptop");
    json.GetProperty("scope").GetString().ShouldBe(scope);
    json.GetProperty("owner").GetString().ShouldBe("holder");
  }

  [Fact]
  public async Task TheCurrentKeyEndpointNeedsAKey()
  {
    (await factory.CreateClientWithKey(null).GetAsync("/api/keys/current")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    var browser = await factory.CreateSignedInBrowserAsync();
    (await browser.GetAsync("/api/keys/current")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }
}
