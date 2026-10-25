using System.Text.Json;
using AgentLedger.Cli.Spooling;
using AgentLedger.Cli.Transcripts;

namespace AgentLedger.Cli.Tests.Transcripts;

// What a hook does for transcripts (ADR 0020): catch up the files the payload names, into the spool.
public sealed class TranscriptCatchUpRun : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly EventSpool _spool;
  private readonly TranscriptStateStore _states;
  private readonly FakeLog _log = new();

  public TranscriptCatchUpRun()
  {
    _spool = new EventSpool(Path.Combine(_dir.Path, "spool", "transcripts"), SpoolLimits.Default);
    _states = new TranscriptStateStore(Path.Combine(_dir.Path, "transcripts"));
  }

  public void Dispose() => _dir.Dispose();

  private TranscriptCatchUp CatchUp() =>
    new("claude-code", new ClaudeCodeTranscriptSource(), _states, new TranscriptReader(1024 * 1024, 4 * 1024 * 1024, _log), _spool, _log);

  private string Payload(string transcript, string? subagent = null) =>
    JsonSerializer.Serialize(new Dictionary<string, string?>
    {
      ["session_id"] = "s1",
      ["transcript_path"] = transcript,
      ["agent_transcript_path"] = subagent,
      ["hook_event_name"] = "Stop",
    });

  // In file order (by first offset): spool names are time-ordered only to the millisecond.
  private List<JsonElement> SpooledBatches() =>
    [.. _spool.List().Select(s => JsonDocument.Parse(_spool.Read(s)!).RootElement)
      .OrderBy(b => b.GetProperty("lines")[0].GetProperty("byteOffset").GetInt64())];

  [Fact]
  public void SpoolsTheNewLinesAndRemembersWhereItStopped()
  {
    var transcript = _dir.Write("s1.jsonl", "one\ntwo\n");

    CatchUp().Run(Payload(transcript));
    System.IO.File.AppendAllText(transcript, "three\n");
    CatchUp().Run(Payload(transcript));

    var batches = SpooledBatches();
    batches.Count.ShouldBe(2);
    batches[0].GetProperty("lines").GetArrayLength().ShouldBe(2);
    batches[1].GetProperty("lines")[0].GetProperty("text").GetString().ShouldBe("three");
  }

  [Fact]
  public void SendsASubagentsLinesWithItsMeta()
  {
    var transcript = _dir.Write("s1.jsonl", "");
    var subagent = _dir.Write(Path.Combine("s1", "subagents", "agent-a1.jsonl"), "sub\n");
    _dir.Write(Path.Combine("s1", "subagents", "agent-a1.meta.json"), """{"agentType":"Explore"}""");

    CatchUp().Run(Payload(transcript, subagent));

    var batch = SpooledBatches().ShouldHaveSingleItem();
    batch.GetProperty("file").GetString().ShouldBe("subagents/agent-a1");
    batch.GetProperty("subagentMeta").GetString().ShouldBe("""{"agentType":"Explore"}""");
  }

  [Fact]
  public void PicksUpASubagentsLateLineOnTheSessionsNextHook()
  {
    // The bug this fixes: the subagent's final line was written after its SubagentStop hook had read the file.
    var transcript = _dir.Write("s1.jsonl", "");
    var subagent = _dir.Write(Path.Combine("s1", "subagents", "agent-a1.jsonl"), "first\n");
    CatchUp().Run(Payload(transcript, subagent)); // SubagentStop

    System.IO.File.AppendAllText(subagent, "final answer\n");
    CatchUp().Run(Payload(transcript)); // the session's next hook, which doesn't name the subagent

    SpooledBatches().Where(b => b.GetProperty("file").GetString() == "subagents/agent-a1")
      .SelectMany(b => b.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("text").GetString()))
      .ShouldBe(["first", "final answer"]);
  }

  [Fact]
  public void SkipsAFileAnotherProcessIsReading()
  {
    var transcript = _dir.Write("s1.jsonl", "one\n");

    using (_states.TryLock(transcript))
    {
      CatchUp().Run(Payload(transcript));
    }

    SpooledBatches().ShouldBeEmpty();
    _states.Load(transcript).ShouldBeNull(); // nothing read, so the next hook starts from the beginning
  }

  [Fact]
  public void DoesNothingForAnAgentWithoutTranscripts()
  {
    var transcript = _dir.Write("s1.jsonl", "one\n");

    new TranscriptCatchUp("claude-code", null, _states, new TranscriptReader(1024, 4096, _log), _spool, _log).Run(Payload(transcript));

    SpooledBatches().ShouldBeEmpty();
  }
}
