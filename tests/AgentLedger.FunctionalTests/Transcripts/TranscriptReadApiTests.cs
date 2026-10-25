using System.Net;
using System.Text.Json;
using AgentLedger.FunctionalTests.Authentication;

namespace AgentLedger.FunctionalTests.Transcripts;

// GET /api/raw/transcripts and /api/raw/transcripts/{lineId}: like the raw events API (ADR 0020, revised).
public sealed class TranscriptReadApiTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  private async Task<JsonElement> GetJsonAsync(string url, HttpClient? client = null)
  {
    client ??= factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read"));
    var response = await client.GetAsync(url);
    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
  }

  [Fact]
  public async Task ListsASessionsLinesNewestFirstInWireTerms()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, """{"type":"user"}"""), (16, """{"type":"assistant"}""")]);

    var json = await GetJsonAsync($"/api/raw/transcripts?session={_session}", await factory.CreateSignedInBrowserAsync());

    var items = json.GetProperty("items");
    items.EnumerateArray().Select(i => i.GetProperty("byteOffset").GetInt64()).ShouldBe([16L, 0L]);
    items[0].GetProperty("agent").GetString().ShouldBe("claude-code");
    items[0].GetProperty("lineType").GetString().ShouldBe("assistant");
    items[0].GetProperty("file").GetString().ShouldBe("main");
    items[0].GetProperty("preview").GetString().ShouldBe("""{"type":"assistant"}""");
  }

  [Fact]
  public async Task FiltersByFileAndLineType()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, """{"type":"assistant"}""")]);
    await TranscriptBatches.SendAsync(ingest, _session, [(0, """{"type":"assistant"}"""), (30, """{"type":"user"}""")], file: "subagents/agent-a1");

    var json = await GetJsonAsync($"/api/raw/transcripts?session={_session}&file=subagents%2Fagent-a1&lineType=assistant");

    var item = json.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
    item.GetProperty("subagentId").GetString().ShouldBe("a1");
  }

  [Fact]
  public async Task PagesWithAnOpaqueCursor()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, "l0"), (3, "l1"), (6, "l2")]);

    var first = await GetJsonAsync($"/api/raw/transcripts?session={_session}&limit=2");
    var second = await GetJsonAsync($"/api/raw/transcripts?session={_session}&limit=2&after={first.GetProperty("nextCursor").GetString()}");

    first.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("preview").GetString()).ShouldBe(["l2", "l1"]);
    second.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("preview").GetString()).ShouldBe(["l0"]);
    second.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  [Fact]
  public async Task GetsOneLineWithItsSenderAndMeta()
  {
    var ingest = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("ingest", "line-sender", "laptop"));
    await TranscriptBatches.SendAsync(ingest, _session, [(7, """{"type":"user",  "x":1}""")], file: "subagents/agent-a1",
      subagentMeta: """{"agentType":"Explore"}""");
    var lineId = (await GetJsonAsync($"/api/raw/transcripts?session={_session}")).GetProperty("items")[0].GetProperty("lineId").GetString();

    var line = await GetJsonAsync($"/api/raw/transcripts/{lineId}");

    line.GetProperty("agent").GetString().ShouldBe("claude-code");
    line.GetProperty("text").GetString().ShouldBe("""{"type":"user",  "x":1}""");
    line.GetProperty("byteOffset").GetInt64().ShouldBe(7);
    line.GetProperty("fileMeta").GetString().ShouldBe("""{"agentType":"Explore"}""");
    line.GetProperty("sentWith").GetProperty("owner").GetString().ShouldBe("line-sender");
    line.GetProperty("sentWith").GetProperty("keyName").GetString().ShouldBe("laptop");
  }

  [Fact]
  public async Task ReturnsNotFoundForAnUnknownLine()
  {
    var reader = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read"));

    (await reader.GetAsync($"/api/raw/transcripts/{Guid.CreateVersion7()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Theory]
  [InlineData("agent=ClaudeCode", "agent")]
  [InlineData("file=nope", "file")]
  [InlineData("after=not-a-cursor", "after")]
  [InlineData("limit=0", "limit")]
  public async Task RejectsInvalidParameters(string query, string field)
  {
    var reader = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read"));

    var response = await reader.GetAsync($"/api/raw/transcripts?{query}");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await response.Content.ReadAsStringAsync()).ShouldContain($"\"{field}\"");
  }

  [Fact]
  public async Task RequiresAReadKeyOrASignedInBrowser()
  {
    var ingestKey = await factory.CreateApiKeyAsync("ingest");

    (await factory.CreateClientWithKey(null).GetAsync("/api/raw/transcripts")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    (await factory.CreateClientWithKey(ingestKey).GetAsync("/api/raw/transcripts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}
