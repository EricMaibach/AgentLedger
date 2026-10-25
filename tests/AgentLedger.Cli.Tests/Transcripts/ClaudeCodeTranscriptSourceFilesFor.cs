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
  public void FindsEverySubagentFileInTheSessionsFolder()
  {
    // Every hook of the session catches up all its subagents, so a line written after a subagent's last hook
    // is still read (ADR 0020, revised).
    var first = _dir.Write(Path.Combine("projects", "-workspace", "s1", "subagents", "agent-a1.jsonl"), "");
    var second = _dir.Write(Path.Combine("projects", "-workspace", "s1", "subagents", "agent-b2.jsonl"), "");

    var files = _source.FilesFor($$"""{"session_id":"s1","transcript_path":"{{MainPath}}","hook_event_name":"PreToolUse"}""");

    files.ShouldBe([
      new TranscriptFileRef(MainPath, "s1", "main", null),
      new TranscriptFileRef(first, "s1", "subagents/agent-a1", Path.ChangeExtension(first, ".meta.json")),
      new TranscriptFileRef(second, "s1", "subagents/agent-b2", Path.ChangeExtension(second, ".meta.json"))], ignoreOrder: true);
  }

  [Fact]
  public void IgnoresOtherFilesInTheSubagentsFolder()
  {
    _dir.Write(Path.Combine("projects", "-workspace", "s1", "subagents", "agent-a1.meta.json"), "{}");
    _dir.Write(Path.Combine("projects", "-workspace", "s1", "subagents", "notes.txt"), "");

    var files = _source.FilesFor($$"""{"session_id":"s1","transcript_path":"{{MainPath}}"}""");

    files.ShouldBe([new TranscriptFileRef(MainPath, "s1", "main", null)]);
  }

  [Fact]
  public void ListsANamedSubagentFileOnlyOnce()
  {
    var subagent = _dir.Write(Path.Combine("projects", "-workspace", "s1", "subagents", "agent-a1.jsonl"), "");

    var files = _source.FilesFor($$"""{"session_id":"s1","transcript_path":"{{MainPath}}","agent_transcript_path":"{{subagent}}"}""");

    files.Count(f => f.FileName == "subagents/agent-a1").ShouldBe(1);
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
