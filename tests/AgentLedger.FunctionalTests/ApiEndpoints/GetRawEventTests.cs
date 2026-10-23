using System.Net;
using System.Text;
using System.Text.Json;
using AgentLedger.FunctionalTests.Authentication;

namespace AgentLedger.FunctionalTests.ApiEndpoints;

public sealed class GetRawEventTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
  // Whitespace, key order and a duplicate key: the detail must return the payload byte-for-byte.
  private const string OddlyFormattedPayload = "{ \"z\": 1,\n  \"a\": [true, null],  \"a\": \"dup\" }";

  private HttpClient _client = default!; // signed in before each test (ADR 0018)
  private HttpClient _ingest = default!;  // sends events with an ingest key, as the CLI does (ADR 0019)

  public async Task InitializeAsync()
  {
    _client = await factory.CreateSignedInBrowserAsync();
    _ingest = await factory.CreateIngestClientAsync();
  }

  public Task DisposeAsync() => Task.CompletedTask;

  private async Task SendAsync(string envelope)
  {
    var response = await _ingest.PostAsync("/events", new StringContent(envelope, Encoding.UTF8, "application/json"));
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
  }

  [Fact]
  public async Task ReturnsAStoredEventWithEveryReceipt()
  {
    var eventId = Guid.CreateVersion7();
    var envelope = TestEnvelope.Json(eventId: eventId, eventType: "UserPromptSubmit", payloadJson: OddlyFormattedPayload);
    await SendAsync(envelope);
    await SendAsync(envelope); // a resend: a second receipt of the same event

    var response = await _client.GetAsync($"/api/raw/events/{eventId}");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var detail = body.RootElement;
    detail.GetProperty("eventId").GetGuid().ShouldBe(eventId);
    detail.GetProperty("agent").GetString().ShouldBe("claude-code"); // the wire name, as on ingest
    detail.GetProperty("eventType").GetString().ShouldBe("UserPromptSubmit");
    detail.GetProperty("host").GetString().ShouldBe("devbox");
    detail.GetProperty("tags").GetProperty("story").GetString().ShouldBe("123");
    detail.GetProperty("payload").GetString().ShouldBe(OddlyFormattedPayload); // exact stored text, as a JSON string
    var receipts = detail.GetProperty("receipts").EnumerateArray().ToList();
    receipts.Count.ShouldBe(2);
    receipts[0].GetProperty("receivedAt").GetDateTimeOffset()
      .ShouldBeLessThanOrEqualTo(receipts[1].GetProperty("receivedAt").GetDateTimeOffset()); // oldest first
  }

  [Fact]
  public async Task ReturnsNotFoundForAnUnknownEvent()
  {
    var response = await _client.GetAsync($"/api/raw/events/{Guid.CreateVersion7()}");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task ReturnsNotFoundForAnIdThatIsNotAGuid()
  {
    var response = await _client.GetAsync("/api/raw/events/not-a-guid");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task RejectsAnEmptyEventId()
  {
    var response = await _client.GetAsync($"/api/raw/events/{Guid.Empty}");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    body.RootElement.GetProperty("errors").TryGetProperty("eventId", out _).ShouldBeTrue();
  }
}
