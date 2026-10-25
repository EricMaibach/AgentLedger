using System.Text.Json;
using AgentLedger.Cli.Envelope;
using AgentLedger.Cli.Hook;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Spooling;
using AgentLedger.Cli.Transcripts;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.Cli.Tests.Hook;

// A hook also catches up the session's transcript, after recording its event (ADR 0020).
public sealed class HookRunnerTranscripts : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly FakeSender _eventSender = new();
  private readonly FakeSender _transcriptSender = new();
  private readonly FakeLog _log = new();
  private readonly EventSpool _eventSpool;
  private readonly EventSpool _transcriptSpool;
  private readonly string _transcript;

  public HookRunnerTranscripts()
  {
    _eventSpool = new EventSpool(Path.Combine(_dir.Path, "spool"), SpoolLimits.Default);
    _transcriptSpool = new EventSpool(Path.Combine(_dir.Path, "spool", "transcripts"), SpoolLimits.Default);
    _transcript = _dir.Write("s1.jsonl", "{\"type\":\"user\"}\n{\"type\":\"assistant\"}\n");
  }

  public void Dispose() => _dir.Dispose();

  private string Payload => JsonSerializer.Serialize(new Dictionary<string, string>
  {
    ["session_id"] = "s1",
    ["transcript_path"] = _transcript,
    ["hook_event_name"] = "Stop",
  });

  private Task RunAsync()
  {
    var transcripts = new TranscriptPipeline(
      new TranscriptCatchUp("claude-code", new ClaudeCodeTranscriptSource(), new TranscriptStateStore(Path.Combine(_dir.Path, "transcripts")),
        new TranscriptReader(TranscriptReader.DefaultMaxChunkBytes, TranscriptReader.DefaultMaxLineBytes, _log), _transcriptSpool, _log),
      new SpoolDrainer(_transcriptSpool, _transcriptSender, _log, itemName: "transcript batch"));
    var runner = new HookRunner(
      new EnvelopeBuilder(new FakeHostEnvironment { CurrentDirectory = _dir.Path }, new FakeTimeProvider(DateTimeOffset.UtcNow)),
      _eventSpool, _eventSender, _log, resendLimit: 5, transcripts);
    return runner.RunAsync("claude-code", Payload, CancellationToken.None);
  }

  [Fact]
  public async Task SendsTheEventAndTheTranscriptsNewLines()
  {
    await RunAsync();

    _eventSender.Sent.ShouldHaveSingleItem();
    var batch = JsonDocument.Parse(_transcriptSender.Sent.ShouldHaveSingleItem()).RootElement;
    batch.GetProperty("lines").GetArrayLength().ShouldBe(2);
    _transcriptSpool.List().ShouldBeEmpty();
  }

  [Fact]
  public async Task SendsOnlyNewLinesNextTime()
  {
    await RunAsync();
    File.AppendAllText(_transcript, "{\"type\":\"system\"}\n");

    await RunAsync();

    var second = JsonDocument.Parse(_transcriptSender.Sent[1]).RootElement;
    second.GetProperty("lines").GetArrayLength().ShouldBe(1);
  }

  [Fact]
  public async Task KeepsTranscriptBatchesWhenTheApiIsUnavailable()
  {
    _eventSender.Respond = _ => new SendOutcome(SendResult.Failed, "Connection refused");

    await RunAsync();

    _transcriptSender.Sent.ShouldBeEmpty();
    _transcriptSpool.List().ShouldHaveSingleItem(); // read, and waiting to be sent
  }

  [Fact]
  public async Task LogsAnUnreadableTranscriptAndCarriesOn()
  {
    // An event left over from an earlier run; resending it comes after the transcript, so it shows the hook carried on.
    _eventSpool.Write(Guid.CreateVersion7(DateTimeOffset.UtcNow.AddMinutes(-5)), """{"earlier":true}""");
    File.Delete(_transcript);
    Directory.CreateDirectory(_transcript); // a folder where the file should be: reading it fails

    await RunAsync();

    _eventSender.Sent.Count.ShouldBe(2);
    _log.Lines.ShouldContain(l => l.StartsWith("ERROR") && l.Contains(_transcript));
  }
}
