using System.Net;
using System.Text.Json;
using AgentLedger.FunctionalTests.Authentication;

namespace AgentLedger.FunctionalTests.Transcripts;

// POST /transcripts/lines (ADR 0020): an ingest key, gzipped batches, each line stored once.
public sealed class TranscriptIngestTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  private static async Task<(int Stored, int AlreadyStored)> CountsAsync(HttpResponseMessage response)
  {
    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    return (json.GetProperty("stored").GetInt32(), json.GetProperty("alreadyStored").GetInt32());
  }

  [Fact]
  public async Task StoresAGzippedBatchAndSkipsLinesItAlreadyHas()
  {
    var ingest = await factory.CreateIngestClientAsync();

    (await CountsAsync(await TranscriptBatches.SendAsync(ingest, _session, [(0, "a"), (2, "b")]))).ShouldBe((2, 0));
    (await CountsAsync(await TranscriptBatches.SendAsync(ingest, _session, [(0, "a"), (2, "b"), (4, "c")]))).ShouldBe((1, 2));
  }

  [Fact]
  public async Task KeepsTheTextExactly()
  {
    var ingest = await factory.CreateIngestClientAsync();
    const string text = "{\"type\":\"user\",  \"text\":\"naïve 🔑 \\u00e9\"} not json";
    await TranscriptBatches.SendAsync(ingest, _session, [(0, text)]);

    var reader = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read"));
    var json = JsonDocument.Parse(await reader.GetStringAsync($"/api/raw/transcripts/lines?session={_session}")).RootElement;

    json.GetProperty("items")[0].GetProperty("text").GetString().ShouldBe(text);
  }

  [Fact]
  public async Task AcceptsAnUncompressedBatchToo()
  {
    var ingest = await factory.CreateIngestClientAsync();

    var response = await ingest.PostAsync("/transcripts/lines", TranscriptBatches.Plain(TranscriptBatches.Json(_session, [(0, "a")])));

    (await CountsAsync(response)).ShouldBe((1, 0));
  }

  [Fact]
  public async Task NeedsAnIngestKey()
  {
    var body = TranscriptBatches.Json(_session, [(0, "a")]);

    (await factory.CreateClientWithKey(null).PostAsync("/transcripts/lines", TranscriptBatches.Gzipped(body)))
      .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    (await factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read")).PostAsync("/transcripts/lines", TranscriptBatches.Gzipped(body)))
      .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Theory]
  [InlineData("ClaudeCode", "main", "agent")]               // the wire name is kebab-case
  [InlineData("claude-code", "/home/x.jsonl", "file")]
  public async Task RejectsAnInvalidEnvelope(string agent, string file, string field)
  {
    var ingest = await factory.CreateIngestClientAsync();

    var response = await ingest.PostAsync("/transcripts/lines",
      TranscriptBatches.Gzipped(TranscriptBatches.Json(_session, [(0, "a")], file: file, agent: agent)));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await response.Content.ReadAsStringAsync()).ShouldContain($"\"{field}\"");
  }

  [Fact]
  public async Task RefusesABatchLargerThanTheLimitOnceDecompressed()
  {
    // Compresses to almost nothing, but decompresses past the limit: refused, not expanded into memory.
    var ingest = await factory.CreateIngestClientAsync();
    var huge = TranscriptBatches.Json(_session, [(0, new string('x', 9 * 1024 * 1024))]);

    var response = await ingest.PostAsync("/transcripts/lines", TranscriptBatches.Gzipped(huge));

    response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
  }
}
