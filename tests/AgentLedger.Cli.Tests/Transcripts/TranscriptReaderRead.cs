using System.Text;
using AgentLedger.Cli.Transcripts;

namespace AgentLedger.Cli.Tests.Transcripts;

// Reading a transcript incrementally (ADR 0020): complete lines only, byte offsets, bounded per run.
public sealed class TranscriptReaderRead : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly FakeLog _log = new();

  public void Dispose() => _dir.Dispose();

  private TranscriptFileRef File(string content, string name = "s1.jsonl")
  {
    var path = Path.Combine(_dir.Path, name);
    System.IO.File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
    return new TranscriptFileRef(path, "s1", "main", null);
  }

  private void Append(TranscriptFileRef file, string content) => System.IO.File.AppendAllText(file.Path, content);

  private TranscriptReader Reader(int maxChunkBytes = 1024 * 1024, int maxLineBytes = 4 * 1024 * 1024) =>
    new(maxChunkBytes, maxLineBytes, _log);

  [Fact]
  public void ReadsCompleteLinesWithTheirByteOffsets()
  {
    var file = File("{\"a\":1}\n{\"b\":2}\n");

    var chunk = Reader().Read(file, state: null);

    chunk.Lines.ShouldBe([new TranscriptLineRead(0, "{\"a\":1}"), new TranscriptLineRead(8, "{\"b\":2}")]);
    chunk.NewState.Offset.ShouldBe(16);
  }

  [Fact]
  public void LeavesAPartlyWrittenLastLineForNextTime()
  {
    var file = File("{\"a\":1}\n{\"b\":");

    var first = Reader().Read(file, state: null);
    Append(file, "2}\n");
    var second = Reader().Read(file, first.NewState);

    first.Lines.Select(l => l.Text).ShouldBe(["{\"a\":1}"]);
    second.Lines.ShouldBe([new TranscriptLineRead(8, "{\"b\":2}")]);
  }

  [Fact]
  public void ContinuesWhereItStopped()
  {
    var file = File("one\n");
    var first = Reader().Read(file, state: null);

    Append(file, "two\n");
    var second = Reader().Read(file, first.NewState);

    second.Lines.ShouldBe([new TranscriptLineRead(4, "two")]);
    Reader().Read(file, second.NewState).Lines.ShouldBeEmpty();
  }

  [Fact]
  public void ReadsAtMostAChunkPerRun()
  {
    var file = File(string.Concat(Enumerable.Range(0, 10).Select(i => $"line-{i}\n"))); // 7 bytes each

    var first = Reader(maxChunkBytes: 30).Read(file, state: null);
    var second = Reader(maxChunkBytes: 30).Read(file, first.NewState);

    first.Lines.Select(l => l.Text).ShouldBe(["line-0", "line-1", "line-2", "line-3"]);
    second.Lines.First().Text.ShouldBe("line-4");
  }

  [Fact]
  public void ReadsALineLongerThanAChunk()
  {
    var file = File(new string('x', 100) + "\nshort\n");

    var chunk = Reader(maxChunkBytes: 30).Read(file, state: null);

    chunk.Lines.ShouldBe([new TranscriptLineRead(0, new string('x', 100))]);
  }

  [Fact]
  public void SkipsALineTooLongToSendAndLogsIt()
  {
    var file = File(new string('x', 200) + "\nafter\n");

    var chunk = Reader(maxChunkBytes: 30, maxLineBytes: 50).Read(file, state: null);
    var next = Reader(maxChunkBytes: 30, maxLineBytes: 50).Read(file, chunk.NewState);

    chunk.Lines.ShouldBeEmpty();
    chunk.NewState.Offset.ShouldBe(201);
    next.Lines.ShouldBe([new TranscriptLineRead(201, "after")]);
    _log.Lines.ShouldContain(l => l.StartsWith("ERROR") && l.Contains("offset 0"));
  }

  [Fact]
  public void CountsOffsetsInBytesNotCharacters()
  {
    var file = File("é🔑\nnext\n"); // é is 2 bytes, 🔑 is 4

    Reader().Read(file, state: null).Lines.ShouldBe([new TranscriptLineRead(0, "é🔑"), new TranscriptLineRead(7, "next")]);
  }

  [Fact]
  public void KeepsTheLineExactlyIncludingACarriageReturnAndEmptyLines()
  {
    var file = File("a\r\n\nb\n");

    Reader().Read(file, state: null).Lines.ShouldBe([
      new TranscriptLineRead(0, "a\r"), new TranscriptLineRead(3, ""), new TranscriptLineRead(4, "b")]);
  }

  [Fact]
  public void StartsAgainWhenTheFileShrank()
  {
    var file = File("one\ntwo\n");
    var state = Reader().Read(file, state: null).NewState;

    System.IO.File.WriteAllText(file.Path, "new\n");
    var chunk = Reader().Read(file, state);

    chunk.Lines.ShouldBe([new TranscriptLineRead(0, "new")]);
  }

  [Fact]
  public void StartsAgainWhenTheFileWasReplaced()
  {
    // Same length or longer, but different from the start: a different file now.
    var file = File("one\ntwo\n");
    var state = Reader().Read(file, state: null).NewState;

    System.IO.File.WriteAllText(file.Path, "ONE\ntwo\nthree\n");
    var chunk = Reader().Read(file, state);

    chunk.Lines.Select(l => l.Text).ShouldBe(["ONE", "two", "three"]);
  }

  [Fact]
  public void ReadsNothingFromAMissingFile()
  {
    var missing = new TranscriptFileRef(Path.Combine(_dir.Path, "gone.jsonl"), "s1", "main", null);

    Reader().Read(missing, state: null).Lines.ShouldBeEmpty();
  }
}
