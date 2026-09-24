using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    var response = await PostAsync(Envelope(eventId: eventId, payloadJson: OddlyFormattedPayload));

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
    var envelope = Envelope(eventId: Guid.CreateVersion7());

    var first = await ReadReceiptIdAsync(await PostAsync(envelope));
    var resend = await ReadReceiptIdAsync(await PostAsync(envelope));

    resend.ShouldNotBe(first);
    (await LoadAsync(resend)).EventId.ShouldBe((await LoadAsync(first)).EventId);
  }

  [Fact]
  public async Task TreatsMissingTagsAsNone()
  {
    var response = await PostAsync(Envelope(includeTags: false));

    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    (await LoadAsync(await ReadReceiptIdAsync(response))).Tags.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("cursor")]      // well-formed, but not a supported agent
  [InlineData("ClaudeCode")]  // the stored name, not the wire name: agents are kebab-case on the wire
  public async Task RejectsAnAgentNotNamedAsOnTheWire(string agent)
  {
    await ShouldBeRejectedAsync(Envelope(agent: agent), "agent");
  }

  [Fact]
  public async Task RejectsAMissingPayload()
  {
    await ShouldBeRejectedAsync(Envelope(includePayload: false), "payload");
  }

  [Fact]
  public async Task RejectsAMissingHost()
  {
    await ShouldBeRejectedAsync(Envelope(host: null), "host");
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

  // The envelope as the CLI will send it. The payload is spliced in as raw text so its exact
  // formatting is what goes over the wire.
  private static string Envelope(
    Guid? eventId = null,
    string agent = "claude-code",
    string? host = "devbox",
    bool includeTags = true,
    bool includePayload = true,
    string payloadJson = """{"session_id":"3829bce8","hook_event_name":"PreToolUse"}""")
  {
    var envelope = new JsonObject
    {
      ["eventId"] = eventId ?? Guid.CreateVersion7(),
      ["agent"] = agent,
      ["eventType"] = "PreToolUse",
      ["nativeSessionId"] = "3829bce8-0000-0000-0000-000000000000",
      ["capturedAt"] = new DateTimeOffset(2026, 9, 24, 9, 0, 0, TimeSpan.Zero),
      ["host"] = host,
      ["user"] = "eric",
      ["projectDir"] = "/workspace",
      ["gitRepo"] = "github.com/EricMaibach/AgentLedger",
      ["gitBranch"] = "main",
    };
    if (includeTags)
    {
      envelope["tags"] = new JsonObject { ["story"] = "123" };
    }

    var json = envelope.ToJsonString();
    return includePayload ? json[..^1] + ",\"payload\":" + payloadJson + "}" : json;
  }
}
