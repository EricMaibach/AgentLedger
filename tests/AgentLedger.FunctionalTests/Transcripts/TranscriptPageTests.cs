using System.Net;
using System.Text;
using AgentLedger.FunctionalTests.ApiEndpoints;
using AgentLedger.FunctionalTests.Authentication;

namespace AgentLedger.FunctionalTests.Transcripts;

// /raw/sessions/{session}/transcript (docs/ui.md, Transcript page).
public sealed class TranscriptPageTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  private async Task<string> PageAsync(string query = "")
  {
    var browser = await factory.CreateSignedInBrowserAsync();
    var response = await browser.GetAsync($"/raw/sessions/{_session}/transcript{query}");
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    return await response.Content.ReadAsStringAsync();
  }

  [Fact]
  public async Task ShowsTheMainFilesLinesInOrder()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, """{"type":"user","n":1}"""), (25, """{"type":"assistant","n":2}""")]);

    var html = await PageAsync();

    html.ShouldContain("<title>Transcript");
    html.ShouldContain("assistant");
    html.IndexOf("&quot;n&quot;:1", StringComparison.Ordinal).ShouldBeLessThan(html.IndexOf("&quot;n&quot;:2", StringComparison.Ordinal));
    html.ShouldContain("<details");
    html.ShouldContain("aria-current=\"page\"");
  }

  [Fact]
  public async Task ShowsTheExactTextOnRequest()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, "{\"type\":\"user\",   \"spaced\":true}")]);

    (await PageAsync("?view=exact")).ShouldContain("{&quot;type&quot;:&quot;user&quot;,   &quot;spaced&quot;:true}");
  }

  [Fact]
  public async Task ShowsASubagentsFileWithItsMeta()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, """{"type":"user"}""")]);
    await TranscriptBatches.SendAsync(ingest, _session, [(0, """{"type":"user","sub":true}""")], file: "subagents/agent-a1b2c3",
      subagentMeta: """{"agentType":"Explore"}""");

    var main = await PageAsync();
    main.ShouldContain("Subagent a1b2c3");
    main.ShouldContain("file=subagents%2Fagent-a1b2c3");

    var sub = await PageAsync("?file=subagents%2Fagent-a1b2c3");
    sub.ShouldContain("&quot;sub&quot;:true");
    sub.ShouldContain("Explore");
  }

  [Fact]
  public async Task OffersTheNextLines()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, Enumerable.Range(0, 3).Select(i => ((long)i * 10, $"line {i}")));

    var html = await PageAsync("?limit=2");

    html.ShouldContain("Next lines");
    html.ShouldContain("after=");
  }

  [Fact]
  public async Task SaysSoWhenTheSessionHasNoTranscript()
  {
    var html = await PageAsync();

    html.ShouldContain("No transcript for this session");
    html.ShouldContain($"/raw/events?session={_session}");
  }

  [Fact]
  public async Task TheEventPageLinksToTheTranscript()
  {
    var ingest = await factory.CreateIngestClientAsync();
    var eventId = Guid.CreateVersion7();
    await ingest.PostAsync("/events", new StringContent(TestEnvelope.Json(eventId: eventId, nativeSessionId: _session), Encoding.UTF8, "application/json"));
    var browser = await factory.CreateSignedInBrowserAsync();

    var html = await (await browser.GetAsync($"/raw/events/{eventId}")).Content.ReadAsStringAsync();

    html.ShouldContain($"/raw/sessions/{_session}/transcript");
  }
}
