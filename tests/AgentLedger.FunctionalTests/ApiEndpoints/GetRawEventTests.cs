using System.Net;
using System.Text;
using System.Text.Json;

namespace AgentLedger.FunctionalTests.ApiEndpoints;

public sealed class GetRawEventTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  // Whitespace, key order and a duplicate key: the detail must return the payload byte-for-byte.
  private const string OddlyFormattedPayload = "{ \"z\": 1,\n  \"a\": [true, null],  \"a\": \"dup\" }";

  private readonly HttpClient _client = factory.CreateClient();

  private async Task SendAsync(string envelope)
  {
    var response = await _client.PostAsync("/events", new StringContent(envelope, Encoding.UTF8, "application/json"));
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
  }

  [Fact]
  public async Task ReturnsAStoredEventWithEveryReceipt()
  {
    var eventId = Guid.CreateVersion7();
    var envelope = TestEnvelope.Json(eventId: eventId, eventType: "UserPromptSubmit", payloadJson: OddlyFormattedPayload);
    await SendAsync(envelope);
    await SendAsync(envelope); // a resend: a second receipt of the same event

    var response = await _client.GetAsync($"/raw/events/{eventId}");

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
    var response = await _client.GetAsync($"/raw/events/{Guid.CreateVersion7()}");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task RejectsAnEmptyEventId()
  {
    var response = await _client.GetAsync($"/raw/events/{Guid.Empty}");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    body.RootElement.GetProperty("errors").TryGetProperty("eventId", out _).ShouldBeTrue();
  }
}
