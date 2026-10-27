using System.Net;
using System.Text;
using AgentLedger.FunctionalTests.ApiEndpoints;
using AgentLedger.FunctionalTests.Authentication;

namespace AgentLedger.FunctionalTests.Pages;

// The "Raw events" pages (ADR 0015, docs/ui.md), requested as a browser would: server-rendered HTML.
// Each test sends events in its own session and filters on it.
public sealed class RawEventPagesTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
  private const string OddlyFormattedPayload = "{\"session_id\":\"s\",  \"z\": 1, \"a\": 2, \"a\": 3}";

  private HttpClient _client = default!; // signed in before each test (ADR 0018)
  private HttpClient _ingest = default!;  // sends events with an ingest key, as the CLI does (ADR 0019)

  public async Task InitializeAsync()
  {
    _client = await factory.CreateSignedInBrowserAsync();
    _ingest = await factory.CreateIngestClientAsync();
  }

  public Task DisposeAsync() => Task.CompletedTask;
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  private async Task<Guid> SendAsync(string eventType = "PreToolUse", Guid? eventId = null, string payload = TestEnvelope.DefaultPayload)
  {
    var id = eventId ?? Guid.CreateVersion7();
    var envelope = TestEnvelope.Json(eventId: id, eventType: eventType, nativeSessionId: _session, payloadJson: payload);
    var response = await _ingest.PostAsync("/events", new StringContent(envelope, Encoding.UTF8, "application/json"));
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    return id;
  }

  private async Task<string> GetPageAsync(string url, HttpStatusCode expected = HttpStatusCode.OK)
  {
    var response = await _client.GetAsync(url);
    response.StatusCode.ShouldBe(expected);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
    return await response.Content.ReadAsStringAsync();
  }

  [Fact]
  public async Task ListsASessionsEventsWithLinksToTheirDetail()
  {
    var eventId = await SendAsync("UserPromptSubmit");

    var html = await GetPageAsync($"/raw/events?session={_session}");

    html.ShouldContain("<title>Raw events");
    html.ShouldContain($"href=\"/raw/events/{eventId}\"");
    html.ShouldContain("Claude Code");          // the agent's display name
    html.ShouldContain("UserPromptSubmit");
    html.ShouldContain("UTC");                  // timestamps are labelled
  }

  [Fact]
  public async Task ShowsTheEventTypeCountsWhenFilteredToASession()
  {
    await SendAsync("PreToolUse");
    await SendAsync("PreToolUse");
    await SendAsync("Stop");

    var html = await GetPageAsync($"/raw/events?session={_session}");

    html.ShouldContain("Event types in this session");
  }

  [Fact]
  public async Task MarksEventsThatArrivedMoreThanOnce()
  {
    var eventId = Guid.CreateVersion7();
    await SendAsync(eventId: eventId);
    await SendAsync(eventId: eventId); // a resend

    var html = await GetPageAsync($"/raw/events?session={_session}");

    html.ShouldContain("×2");
  }

  [Fact]
  public async Task OffersOlderEventsWhenThereIsAnotherPage()
  {
    await SendAsync();
    await SendAsync();

    var html = await GetPageAsync($"/raw/events?session={_session}&limit=1");

    html.ShouldContain("Older");
    html.ShouldContain("after=");
  }

  [Fact]
  public async Task SaysSoWhenNothingMatches()
  {
    var html = await GetPageAsync($"/raw/events?session={_session}");

    html.ShouldContain("No events match these filters");
    html.ShouldContain("Clear filters");
  }

  [Fact]
  public async Task ExplainsAnInvalidFilterInsteadOfFailing()
  {
    var html = await GetPageAsync("/raw/events?agent=ClaudeCode");

    html.ShouldContain("kebab-case");
  }

  [Theory]
  [InlineData("after=not-a-cursor", "Start again from the newest events")]
  [InlineData("from=yesterday-ish", "is not a date and time")]
  [InlineData("limit=lots", "is not a number")]
  [InlineData("limit=1000", "limit")]
  [InlineData("from=2026-10-02T00:00&to=2026-10-01T00:00", "role=\"alert\"")]
  public async Task ExplainsOtherUnusableParametersInsteadOfFailing(string query, string expected)
  {
    var html = await GetPageAsync($"/raw/events?{query}");

    html.ShouldContain("role=\"alert\"");
    html.ShouldContain(expected);
  }

  [Fact]
  public async Task ShowsTheCommitTheEventWasBasedOn()
  {
    var eventId = Guid.CreateVersion7();
    var envelope = TestEnvelope.Json(eventId: eventId, nativeSessionId: _session, gitCommit: "2376609a3f019662cf2f5324c09cdb4b61e3be1e");
    (await _ingest.PostAsync("/events", new StringContent(envelope, Encoding.UTF8, "application/json"))).StatusCode.ShouldBe(HttpStatusCode.Created);

    var html = await GetPageAsync($"/raw/events/{eventId}");

    html.ShouldContain("Git commit");
    html.ShouldContain("2376609a3f019662cf2f5324c09cdb4b61e3be1e");
  }

  [Fact]
  public async Task ShowsAnEventsDetailWithItsPayloadFormatted()
  {
    var eventId = await SendAsync("Stop", payload: OddlyFormattedPayload);

    var html = await GetPageAsync($"/raw/events/{eventId}");

    html.ShouldContain("<title>Stop");
    html.ShouldContain(eventId.ToString());
    html.ShouldContain("&quot;a&quot;: 2,"); // formatted: duplicate keys kept, indented
    html.ShouldContain("&quot;a&quot;: 3");
  }

  [Fact]
  public async Task ShowsThePayloadExactlyAsStoredOnRequest()
  {
    var eventId = await SendAsync("Stop", payload: OddlyFormattedPayload);

    var html = await GetPageAsync($"/raw/events/{eventId}?view=exact");

    html.ShouldContain(WebUtility.HtmlEncode(OddlyFormattedPayload));
  }

  [Fact]
  public async Task ListsEveryReceiptOfAnEvent()
  {
    var eventId = Guid.CreateVersion7();
    await SendAsync(eventId: eventId);
    await SendAsync(eventId: eventId);

    var html = await GetPageAsync($"/raw/events/{eventId}");

    html.ShouldContain("Receipts (2)");
  }

  [Fact]
  public async Task AnUnknownEventIsANotFoundPage()
  {
    var html = await GetPageAsync($"/raw/events/{Guid.CreateVersion7()}", HttpStatusCode.NotFound);

    html.ShouldContain("<title>Not found");
    html.ShouldContain("no event with this ID", Case.Sensitive);
    html.ShouldContain($"href=\"/raw/events\"");
  }
}
