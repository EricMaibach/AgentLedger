using System.Net;
using System.Text;
using System.Text.Json;
using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentLedger.FunctionalTests.ApiEndpoints;

public sealed class IngestEventTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  // Whitespace, key order and a duplicate key: must reach the database byte-for-byte (ADR 0009).
  private const string OddlyFormattedPayload = "{ \"z\": 1,\n  \"a\": [true, null],  \"a\": \"duplicate key\" }";

  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task StoresAValidEnvelopeWithItsPayloadExactlyAsSent()
  {
    var eventId = Guid.CreateVersion7();

    var response = await PostAsync(TestEnvelope.Json(eventId: eventId, payloadJson: OddlyFormattedPayload));

    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    var receiptId = await ReadReceiptIdAsync(response);
    var stored = await LoadAsync(receiptId);
    stored.EventId.ShouldBe(AgentEventId.From(eventId));
    stored.Agent.ShouldBe(AgentKind.ClaudeCode);
    stored.Payload.ShouldBe(OddlyFormattedPayload);
  }

  [Fact]
  public async Task StoresAResendAsASecondReceipt()
  {
    var envelope = TestEnvelope.Json(eventId: Guid.CreateVersion7());

    var first = await ReadReceiptIdAsync(await PostAsync(envelope));
    var resend = await ReadReceiptIdAsync(await PostAsync(envelope));

    resend.ShouldNotBe(first);
    (await LoadAsync(resend)).EventId.ShouldBe((await LoadAsync(first)).EventId);
  }

  [Fact]
  public async Task TreatsMissingTagsAsNone()
  {
    var response = await PostAsync(TestEnvelope.Json(includeTags: false));

    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    (await LoadAsync(await ReadReceiptIdAsync(response))).Tags.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("cursor")]      // well-formed, but not a supported agent
  [InlineData("ClaudeCode")]  // the stored name, not the wire name: agents are kebab-case on the wire
  public async Task RejectsAnAgentNotNamedAsOnTheWire(string agent)
  {
    await ShouldBeRejectedAsync(TestEnvelope.Json(agent: agent), "agent");
  }

  [Fact]
  public async Task RejectsAMissingPayload()
  {
    await ShouldBeRejectedAsync(TestEnvelope.Json(includePayload: false), "payload");
  }

  [Fact]
  public async Task RejectsAMissingHost()
  {
    await ShouldBeRejectedAsync(TestEnvelope.Json(host: null), "host");
  }

  private Task<HttpResponseMessage> PostAsync(string json) =>
    _client.PostAsync("/events", new StringContent(json, Encoding.UTF8, "application/json"));

  private async Task ShouldBeRejectedAsync(string envelope, string field)
  {
    var response = await PostAsync(envelope);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    body.RootElement.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
  }

  private static async Task<ReceiptId> ReadReceiptIdAsync(HttpResponseMessage response)
  {
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return ReceiptId.From(body.RootElement.GetProperty("receiptId").GetGuid());
  }

  private async Task<AgentEventReceipt> LoadAsync(ReceiptId id)
  {
    using var scope = factory.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AgentEventReceipts.SingleAsync(r => r.Id == id);
  }

}
