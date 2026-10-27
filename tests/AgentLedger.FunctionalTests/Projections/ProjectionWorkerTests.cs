using AgentLedger.FunctionalTests.Authentication;
using AgentLedger.FunctionalTests.Transcripts;
using AgentLedger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentLedger.FunctionalTests.Projections;

// The projection worker in the running app (ADR 0022): raw rows sent to the ledger become sessions, turns and steps
// within seconds, without anything calling it.
public sealed class ProjectionWorkerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly CustomWebApplicationFactory<Program> _factory;
  private readonly string _session = $"session-{Guid.NewGuid():N}";

  public ProjectionWorkerTests(CustomWebApplicationFactory<Program> factory)
  {
    _factory = factory;
    // A short commit lag and frequent passes, so the test needn't wait long. Not zero: without a lag, a pass could
    // move the watermark past rows still being committed (docs/architecture.md, incremental processing).
    _factory.UseSetting("Projections:Lag", "00:00:01");
    _factory.UseSetting("Projections:Interval", "00:00:00.100");
    // The host starts before the test migrates the database, so the first pass can fail.
    _factory.UseSetting("Projections:RetryDelay", "00:00:00.200");
  }

  [Fact]
  public async Task ProjectsTranscriptLinesIntoATurn()
  {
    var ingest = await _factory.CreateIngestClientAsync();
    const string prompt =
      """{"type":"user","promptId":"p-1","promptSource":"typed","timestamp":"2026-10-26T14:00:00.000Z","message":{"role":"user","content":"Fix the cart"}}""";
    const string answer =
      """{"type":"assistant","timestamp":"2026-10-26T14:00:03.000Z","message":{"id":"msg_1","model":"claude-opus-5-5","role":"assistant","stop_reason":"end_turn","content":[{"type":"text","text":"Fixed."}],"usage":{"input_tokens":3,"output_tokens":20}}}""";

    var response = await TranscriptBatches.SendAsync(ingest, _session, [(0, prompt), (prompt.Length + 1, answer)]);
    response.EnsureSuccessStatusCode();

    var steps = await WaitForStepsAsync(expected: 2);
    steps.ShouldBe([("Prompt", "Fix the cart"), ("Response", "Fixed.")]);
  }

  private async Task<List<(string Kind, string? Text)>> WaitForStepsAsync(int expected)
  {
    var deadline = DateTime.UtcNow.AddSeconds(30);
    while (true)
    {
      using var scope = _factory.Services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var session = await db.Sessions.SingleOrDefaultAsync(s => s.NativeSessionId == _session);
      if (session is not null)
      {
        var steps = await db.Steps.Where(s => s.SessionId == session.Id).OrderBy(s => s.Sequence)
          .Select(s => new { s.Kind, s.Text }).ToListAsync();
        if (steps.Count >= expected || DateTime.UtcNow > deadline)
          return [.. steps.Select(s => (s.Kind, s.Text))];
      }
      else if (DateTime.UtcNow > deadline)
      {
        throw new TimeoutException($"Session {_session} wasn't projected within 30 seconds.");
      }
      await Task.Delay(100);
    }
  }
}
