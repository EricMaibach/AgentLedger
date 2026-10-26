using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentLedger.FunctionalTests.Authentication;
using AgentLedger.FunctionalTests.Transcripts;

namespace AgentLedger.FunctionalTests.Git;

// POST /git/observations (ADR 0021): an ingest key; each commit stored once.
public sealed class GitObservationsTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly string _repository = $"github.com/owner/repo-{Guid.NewGuid():N}";

  private static JsonObject Commit(string hash, params string[] parents) => new()
  {
    ["hash"] = hash,
    ["parents"] = new JsonArray([.. parents.Select(p => (JsonNode)p)]),
    ["authoredAt"] = "2026-10-26T01:00:00Z",
    ["committedAt"] = "2026-10-26T01:01:00Z",
    ["message"] = "Add login\n\nDetails.\n",
    ["files"] = new JsonArray(new JsonObject { ["path"] = "src/Login.cs", ["change"] = "added", ["previousPath"] = null }),
    ["filesTruncated"] = false,
  };

  private string Batch(params JsonObject[] commits) =>
    new JsonObject { ["repository"] = _repository, ["commits"] = new JsonArray([.. commits]) }.ToJsonString();

  private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

  private static async Task<(int Stored, int AlreadyStored)> CountsAsync(HttpResponseMessage response)
  {
    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    return (json.GetProperty("commitsStored").GetInt32(), json.GetProperty("commitsAlreadyStored").GetInt32());
  }

  private async Task<int> TagsRecordedAsync(HttpResponseMessage response)
  {
    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("tagObservationsRecorded").GetInt32();
  }

  [Fact]
  public async Task StoresNewCommitsAndSkipsOnesItHas()
  {
    var ingest = await factory.CreateIngestClientAsync();
    var first = Commit("3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba", "03352b0d8d9391e5508b8f1cc994c2ff99686886");

    (await CountsAsync(await ingest.PostAsync("/git/observations", Json(Batch(first))))).ShouldBe((1, 0));
    (await CountsAsync(await ingest.PostAsync("/git/observations",
      Json(Batch(Commit("3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba", "03352b0d8d9391e5508b8f1cc994c2ff99686886"),
        Commit("4c49c1ea57c878d3a185d98340c599d6afffe9bd", "3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba")))))).ShouldBe((1, 1));
  }

  [Fact]
  public async Task AcceptsAGzippedBatch()
  {
    var ingest = await factory.CreateIngestClientAsync();

    var response = await ingest.PostAsync("/git/observations", TranscriptBatches.Gzipped(Batch(Commit("5555555555555555555555555555555555555555"))));

    (await CountsAsync(response)).ShouldBe((1, 0));
  }

  [Fact]
  public async Task NeedsAnIngestKey()
  {
    var body = Batch(Commit("6666666666666666666666666666666666666666"));

    (await factory.CreateClientWithKey(null).PostAsync("/git/observations", Json(body))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    (await factory.CreateClientWithKey(await factory.CreateApiKeyAsync("read")).PostAsync("/git/observations", Json(body)))
      .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task RejectsAnInvalidCommitByField()
  {
    var ingest = await factory.CreateIngestClientAsync();

    var response = await ingest.PostAsync("/git/observations", Json(Batch(Commit("not-a-hash"))));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await response.Content.ReadAsStringAsync()).ShouldContain("\"commits\"");
  }

  [Fact]
  public async Task RecordsTagsWithoutCommits()
  {
    var ingest = await factory.CreateIngestClientAsync();
    var body = new JsonObject
    {
      ["repository"] = _repository,
      ["commits"] = new JsonArray(),
      ["tags"] = new JsonArray(
        new JsonObject { ["name"] = "v0.4.3", ["change"] = "existing", ["commit"] = "eb91b9901b8f78138273bb00d40f097b953534eb", ["observedAt"] = "2026-10-26T02:14:19Z" },
        new JsonObject { ["name"] = "v0.4.2", ["change"] = "deleted", ["previousCommit"] = "3e2d081f2042d75a1ef93e3611cdbb7f2fccdbba", ["observedAt"] = "2026-10-26T02:14:19Z" }),
    }.ToJsonString();

    (await TagsRecordedAsync(await ingest.PostAsync("/git/observations", Json(body)))).ShouldBe(2);
  }

  [Fact]
  public async Task RejectsAnInvalidTagByField()
  {
    var ingest = await factory.CreateIngestClientAsync();
    var body = new JsonObject
    {
      ["repository"] = _repository,
      ["tags"] = new JsonArray(new JsonObject { ["name"] = "v1", ["change"] = "moved", ["commit"] = "eb91b9901b8f78138273bb00d40f097b953534eb", ["observedAt"] = "2026-10-26T02:14:19Z" }),
    }.ToJsonString();

    var response = await ingest.PostAsync("/git/observations", Json(body));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await response.Content.ReadAsStringAsync()).ShouldContain("\"tags\"");
  }

  [Fact]
  public async Task RecordsATagObservationOnceWhenTheSameBatchArrivesTwice()
  {
    var ingest = await factory.CreateIngestClientAsync();
    var body = new JsonObject
    {
      ["repository"] = _repository,
      ["tags"] = new JsonArray(new JsonObject
      {
        ["id"] = Guid.CreateVersion7(),
        ["name"] = "v0.5.0",
        ["change"] = "created",
        ["commit"] = "eb91b9901b8f78138273bb00d40f097b953534eb",
        ["observedAt"] = "2026-10-26T03:00:00Z",
      }),
    }.ToJsonString();

    var first = await ingest.PostAsync("/git/observations", Json(body));
    var second = await ingest.PostAsync("/git/observations", Json(body));

    (await TagsRecordedAsync(first)).ShouldBe(1);
    var json = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement;
    json.GetProperty("tagObservationsRecorded").GetInt32().ShouldBe(0);
    json.GetProperty("tagObservationsAlreadyRecorded").GetInt32().ShouldBe(1);
  }
}
