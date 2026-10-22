using System.Net;
using System.Text;
using System.Text.Json;

namespace AgentLedger.FunctionalTests.ApiEndpoints;

// GET /api/raw/events and GET /api/raw/events/counts (ADR 0016, 0017). Each test sends events in its own session
// and filters on it, so tests sharing the database don't see each other's events.
public sealed class ListRawEventsTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly HttpClient _client = factory.CreateClient();
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  private async Task<Guid> SendAsync(string eventType = "PreToolUse", Guid? eventId = null)
  {
    var id = eventId ?? Guid.CreateVersion7();
    var envelope = TestEnvelope.Json(eventId: id, eventType: eventType, nativeSessionId: _session);
    var response = await _client.PostAsync("/events", new StringContent(envelope, Encoding.UTF8, "application/json"));
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    return id;
  }

  private async Task<JsonElement> GetJsonAsync(string url)
  {
    var response = await _client.GetAsync(url);
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
  }

  [Fact]
  public async Task ListsASessionsEventsNewestFirstInWireTerms()
  {
    var first = await SendAsync("SessionStart");
    var second = await SendAsync("UserPromptSubmit");

    var page = await GetJsonAsync($"/api/raw/events?session={_session}");

    var items = page.GetProperty("items").EnumerateArray().ToList();
    items.Select(i => i.GetProperty("eventId").GetGuid()).ShouldBe([second, first]);
    items.ShouldAllBe(i => i.GetProperty("agent").GetString() == "claude-code");
    items[0].GetProperty("receiptCount").GetInt32().ShouldBe(1);
    items[0].GetProperty("preview").GetString()!.ShouldStartWith("{\"session_id\"");
    page.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  [Fact]
  public async Task PagesWithAnOpaqueCursor()
  {
    var oldest = await SendAsync();
    var middle = await SendAsync();
    var newest = await SendAsync();

    var firstPage = await GetJsonAsync($"/api/raw/events?session={_session}&limit=2");
    var cursor = firstPage.GetProperty("nextCursor").GetString();
    var secondPage = await GetJsonAsync($"/api/raw/events?session={_session}&limit=2&after={Uri.EscapeDataString(cursor!)}");

    firstPage.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("eventId").GetGuid()).ShouldBe([newest, middle]);
    secondPage.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("eventId").GetGuid()).ShouldBe([oldest]);
    secondPage.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  [Fact]
  public async Task ShowsResendsOnlyWhenAsked()
  {
    var eventId = Guid.CreateVersion7();
    await SendAsync(eventId: eventId);
    await SendAsync(eventId: eventId); // a resend

    var distinct = await GetJsonAsync($"/api/raw/events?session={_session}");
    var everything = await GetJsonAsync($"/api/raw/events?session={_session}&distinct=false");

    distinct.GetProperty("items").GetArrayLength().ShouldBe(1);
    distinct.GetProperty("items")[0].GetProperty("receiptCount").GetInt32().ShouldBe(2);
    everything.GetProperty("items").GetArrayLength().ShouldBe(2);
  }

  [Fact]
  public async Task FiltersByAgentUsingItsWireName()
  {
    await SendAsync();

    var claude = await GetJsonAsync($"/api/raw/events?session={_session}&agent=claude-code");
    var codex = await GetJsonAsync($"/api/raw/events?session={_session}&agent=codex");

    claude.GetProperty("items").GetArrayLength().ShouldBe(1);
    codex.GetProperty("items").GetArrayLength().ShouldBe(0);
  }

  [Theory]
  [InlineData("agent=ClaudeCode", "agent")]  // the stored name, not the wire name
  [InlineData("agent=cursor", "agent")]      // not a supported agent
  [InlineData("after=not-a-cursor", "after")]
  [InlineData("limit=0", "limit")]
  public async Task RejectsInvalidParameters(string query, string field)
  {
    var response = await _client.GetAsync($"/api/raw/events?{query}");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    body.RootElement.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
  }

  [Fact]
  public async Task CountsASessionsEventsByType()
  {
    await SendAsync("PreToolUse");
    await SendAsync("PreToolUse");
    await SendAsync("Stop");

    var counts = await GetJsonAsync($"/api/raw/events/counts?session={_session}");

    counts.EnumerateArray()
      .Select(c => (c.GetProperty("eventType").GetString(), c.GetProperty("count").GetInt32()))
      .ShouldBe([("PreToolUse", 2), ("Stop", 1)]);
  }
}
