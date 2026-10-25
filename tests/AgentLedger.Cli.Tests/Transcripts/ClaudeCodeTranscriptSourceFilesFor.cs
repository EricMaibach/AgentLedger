using AgentLedger.Cli.Transcripts;

namespace AgentLedger.Cli.Tests.Transcripts;

// Which transcript files a Claude Code hook payload points to (ADR 0020).
public sealed class ClaudeCodeTranscriptSourceFilesFor : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly ClaudeCodeTranscriptSource _source = new();

  public void Dispose() => _dir.Dispose();

  private string MainPath => Path.Combine(_dir.Path, "projects", "-workspace", "s1.jsonl");

  [Fact]
  public void FindsTheSessionsMainTranscript()
  {
    var files = _source.FilesFor($$"""{"session_id":"s1","transcript_path":"{{MainPath}}","hook_event_name":"Stop"}""");

    files.ShouldBe([new TranscriptFileRef(MainPath, "s1", "main", MetaPath: null)]);
  }

  [Fact]
  public void FindsASubagentsTranscriptAndMetaWhenItStops()
  {
    var subagent = Path.Combine(_dir.Path, "projects", "-workspace", "s1", "subagents", "agent-a1b2c3.jsonl");

    var files = _source.FilesFor($$"""
      {"session_id":"s1","transcript_path":"{{MainPath}}","agent_transcript_path":"{{subagent}}","hook_event_name":"SubagentStop"}
      """);

    files.ShouldBe([
      new TranscriptFileRef(MainPath, "s1", "main", null),
      new TranscriptFileRef(subagent, "s1", "subagents/agent-a1b2c3", Path.ChangeExtension(subagent, ".meta.json"))]);
  }

  [Fact]
  public void FindsTheSubagentsTranscriptFromInsideIt()
  {
    // Hooks fired inside a subagent carry its agent_id; its file sits in the session's subagents folder.
    var subagent = _dir.Write(Path.Combine("projects", "-workspace", "s1", "subagents", "agent-a1b2c3.jsonl"), "");

    var files = _source.FilesFor($$"""{"session_id":"s1","transcript_path":"{{MainPath}}","agent_id":"a1b2c3","hook_event_name":"PreToolUse"}""");

    files.ShouldContain(new TranscriptFileRef(subagent, "s1", "subagents/agent-a1b2c3", Path.ChangeExtension(subagent, ".meta.json")));
  }

  [Fact]
  public void DoesNotGuessASubagentFileThatIsNotThere()
  {
    var files = _source.FilesFor($$"""{"session_id":"s1","transcript_path":"{{MainPath}}","agent_id":"a1b2c3"}""");

    files.ShouldBe([new TranscriptFileRef(MainPath, "s1", "main", null)]);
  }

  [Theory]
  [InlineData("""{"hook_event_name":"Stop"}""")]                                   // no transcript
  [InlineData("""{"transcript_path":"/x/s1.jsonl"}""")]                            // no session
  [InlineData("not json")]
  public void FindsNothingWithoutASessionAndTranscript(string payload)
  {
    _source.FilesFor(payload).ShouldBeEmpty();
  }

  [Fact]
  public void IgnoresASubagentFileWithAnUnexpectedName()
  {
    var files = _source.FilesFor($$"""{"session_id":"s1","transcript_path":"{{MainPath}}","agent_transcript_path":"/x/weird name.jsonl"}""");

    files.ShouldBe([new TranscriptFileRef(MainPath, "s1", "main", null)]);
  }

  [Fact]
  public void IsKnownOnlyForClaudeCodeSoFar()
  {
    TranscriptSources.For("claude-code").ShouldBeOfType<ClaudeCodeTranscriptSource>();
    TranscriptSources.For("codex").ShouldBeNull();
  }
}
