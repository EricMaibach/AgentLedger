using System.Net;
using System.Text;
using System.Text.Json;
using AgentLedger.FunctionalTests.ApiEndpoints;
using AgentLedger.FunctionalTests.Authentication;

namespace AgentLedger.FunctionalTests.Transcripts;

// The "Raw transcripts" pages (docs/ui.md): built like the raw events pages.
public sealed class TranscriptPageTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  private async Task<string> PageAsync(string url, HttpStatusCode expected = HttpStatusCode.OK)
  {
    var browser = await factory.CreateSignedInBrowserAsync();
    var response = await browser.GetAsync(url);
    response.StatusCode.ShouldBe(expected);
    return await response.Content.ReadAsStringAsync();
  }

  private async Task<string> LineIdAsync(int index = 0)
  {
    var reader = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read"));
    var json = JsonDocument.Parse(await reader.GetStringAsync($"/api/raw/transcripts?session={_session}")).RootElement;
    return json.GetProperty("items")[index].GetProperty("lineId").GetString()!;
  }

  [Fact]
  public async Task ListsASessionsLinesWithLinksToTheirPage()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, """{"type":"user"}"""), (16, """{"type":"assistant"}""")]);

    var html = await PageAsync($"/raw/transcripts?session={_session}");

    html.ShouldContain("<title>Raw transcripts");
    html.ShouldContain($"href=\"/raw/transcripts/{await LineIdAsync()}\"");
    html.ShouldContain("Claude Code");
    html.ShouldContain("assistant");
    html.ShouldContain("UTC");
  }

  [Fact]
  public async Task OffersOlderLinesWhenThereIsAnotherPage()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, "a"), (2, "b")]);

    var html = await PageAsync($"/raw/transcripts?session={_session}&limit=1");

    html.ShouldContain("Older");
    html.ShouldContain("after=");
  }

  [Fact]
  public async Task SaysSoWhenNothingMatches()
  {
    var html = await PageAsync($"/raw/transcripts?session={_session}");

    html.ShouldContain("No transcript lines match these filters");
    html.ShouldContain("Clear filters");
  }

  [Fact]
  public async Task ExplainsAnUnusableFilterInsteadOfFailing()
  {
    (await PageAsync("/raw/transcripts?file=nope")).ShouldContain("role=\"alert\"");
  }

  [Fact]
  public async Task ShowsALineWithWhoSentItFormatted()
  {
    var ingest = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("ingest", "page-sender", "laptop"));
    await TranscriptBatches.SendAsync(ingest, _session, [(412, """{"type":"user","a":2}""")]);

    var html = await PageAsync($"/raw/transcripts/{await LineIdAsync()}");

    html.ShouldContain("<title>user");
    html.ShouldContain("412");
    html.ShouldContain("page-sender");
    html.ShouldContain("laptop");
    html.ShouldContain("&quot;a&quot;: 2");
  }

  [Fact]
  public async Task ShowsALineExactlyOnRequest()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, "{\"type\":\"user\",   \"spaced\":true}")]);

    (await PageAsync($"/raw/transcripts/{await LineIdAsync()}?view=exact"))
      .ShouldContain("{&quot;type&quot;:&quot;user&quot;,   &quot;spaced&quot;:true}");
  }

  [Fact]
  public async Task ShowsASubagentLinesMeta()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, """{"type":"user"}""")], file: "subagents/agent-a1b2c3",
      subagentMeta: """{"agentType":"Explore"}""");

    var html = await PageAsync($"/raw/transcripts/{await LineIdAsync()}");

    html.ShouldContain("a1b2c3");
    html.ShouldContain("Explore");
  }

  [Fact]
  public async Task AnUnknownLineIsANotFoundPage()
  {
    (await PageAsync($"/raw/transcripts/{Guid.CreateVersion7()}", HttpStatusCode.NotFound)).ShouldContain("<title>Not found");
  }

  [Fact]
  public async Task TheOldSessionTranscriptUrlStillWorks()
  {
    var browser = await factory.CreateSignedInBrowserAsync();

    var response = await browser.GetAsync($"/raw/sessions/{_session}/transcript");

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.RedirectTarget().ShouldBe($"/raw/transcripts?session={_session}");
  }

  [Fact]
  public async Task EventsAndTranscriptLinesLinkToEachOther()
  {
    var ingest = await factory.CreateIngestClientAsync();
    var eventId = Guid.CreateVersion7();
    await ingest.PostAsync("/events", new StringContent(TestEnvelope.Json(eventId: eventId, nativeSessionId: _session), Encoding.UTF8, "application/json"));
    await TranscriptBatches.SendAsync(ingest, _session, [(0, """{"type":"user"}""")]);

    (await PageAsync($"/raw/events/{eventId}")).ShouldContain($"/raw/transcripts?session={_session}");
    (await PageAsync($"/raw/transcripts/{await LineIdAsync()}")).ShouldContain($"/raw/events?session={_session}");
  }

  [Fact]
  public async Task TheNavigationLeadsToRawTranscripts()
  {
    (await PageAsync("/raw/events")).ShouldContain("href=\"/raw/transcripts\"");
  }
}
