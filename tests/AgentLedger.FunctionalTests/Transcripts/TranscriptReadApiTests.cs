using System.Net;
using System.Text.Json;
using AgentLedger.FunctionalTests.Authentication;

namespace AgentLedger.FunctionalTests.Transcripts;

// GET /api/raw/transcripts and /api/raw/transcripts/lines (ADR 0020): a signed-in browser or a read key.
public sealed class TranscriptReadApiTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  private async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
  {
    var response = await client.GetAsync(url);
    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
  }

  [Fact]
  public async Task ListsASessionsFiles()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, "a"), (2, "b")]);
    await TranscriptBatches.SendAsync(ingest, _session, [(0, "x")], file: "subagents/agent-a1b2c3", subagentMeta: """{"agentType":"Explore"}""");

    var json = await GetJsonAsync(await factory.CreateSignedInBrowserAsync(), $"/api/raw/transcripts?session={_session}");

    var files = json.GetProperty("files");
    files.GetArrayLength().ShouldBe(2);
    files[0].GetProperty("agent").GetString().ShouldBe("claude-code");
    files[0].GetProperty("file").GetString().ShouldBe("main");
    files[0].GetProperty("lineCount").GetInt32().ShouldBe(2);
    files[1].GetProperty("subagentId").GetString().ShouldBe("a1b2c3");
    files[1].GetProperty("meta").GetString().ShouldBe("""{"agentType":"Explore"}""");
  }

  [Fact]
  public async Task PagesThroughAFilesLines()
  {
    var ingest = await factory.CreateIngestClientAsync();
    await TranscriptBatches.SendAsync(ingest, _session, [(0, "l0"), (3, "l1"), (6, "l2")]);
    var reader = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read"));

    var first = await GetJsonAsync(reader, $"/api/raw/transcripts/lines?session={_session}&limit=2");
    var cursor = first.GetProperty("nextCursor").GetString();
    var second = await GetJsonAsync(reader, $"/api/raw/transcripts/lines?session={_session}&limit=2&after={cursor}");

    first.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("text").GetString()).ShouldBe(["l0", "l1"]);
    first.GetProperty("items")[1].GetProperty("byteOffset").GetInt64().ShouldBe(3);
    second.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("text").GetString()).ShouldBe(["l2"]);
    second.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  [Theory]
  [InlineData("file=nope", "file")]
  [InlineData("after=not-a-cursor", "after")]
  [InlineData("limit=0", "limit")]
  public async Task RejectsInvalidParameters(string query, string field)
  {
    var reader = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read"));

    var response = await reader.GetAsync($"/api/raw/transcripts/lines?session={_session}&{query}");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await response.Content.ReadAsStringAsync()).ShouldContain($"\"{field}\"");
  }

  [Fact]
  public async Task RequiresAReadKeyOrASignedInBrowser()
  {
    var ingestKey = await factory.CreateApiKeyAsync("ingest");

    (await factory.CreateClientWithKey(null).GetAsync($"/api/raw/transcripts?session={_session}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    (await factory.CreateClientWithKey(ingestKey).GetAsync($"/api/raw/transcripts?session={_session}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}
