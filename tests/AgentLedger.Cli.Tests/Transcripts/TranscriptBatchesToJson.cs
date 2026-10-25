using System.Text.Json;
using AgentLedger.Cli.Transcripts;

namespace AgentLedger.Cli.Tests.Transcripts;

// A chunk becomes batches in the server's request format (POST /transcripts/lines, ADR 0020).
public sealed class TranscriptBatchesToJson
{
  private static readonly TranscriptFileRef Main = new("/x/s1.jsonl", "s1", "main", null);

  [Fact]
  public void WritesTheServersFormat()
  {
    var json = TranscriptBatches.ToJson("claude-code", Main, meta: null, [new TranscriptLineRead(0, "{\"a\":1}"), new TranscriptLineRead(8, "x")])
      .ShouldHaveSingleItem();

    var root = JsonDocument.Parse(json).RootElement;
    root.GetProperty("agent").GetString().ShouldBe("claude-code");
    root.GetProperty("nativeSessionId").GetString().ShouldBe("s1");
    root.GetProperty("file").GetString().ShouldBe("main");
    root.GetProperty("subagentMeta").ValueKind.ShouldBe(JsonValueKind.Null);
    root.GetProperty("lines")[1].GetProperty("byteOffset").GetInt64().ShouldBe(8);
    root.GetProperty("lines")[0].GetProperty("text").GetString().ShouldBe("{\"a\":1}");
  }

  [Fact]
  public void IncludesASubagentsMetaExactly()
  {
    var subagent = new TranscriptFileRef("/x/s1/subagents/agent-a1.jsonl", "s1", "subagents/agent-a1", "/x/meta");

    var json = TranscriptBatches.ToJson("claude-code", subagent, """{"agentType":"Explore",  "x":1}""", [new TranscriptLineRead(0, "a")]).Single();

    JsonDocument.Parse(json).RootElement.GetProperty("subagentMeta").GetString().ShouldBe("""{"agentType":"Explore",  "x":1}""");
  }

  [Fact]
  public void SplitsAtTheLineLimit()
  {
    var lines = Enumerable.Range(0, TranscriptBatches.MaxLines + 1).Select(i => new TranscriptLineRead(i * 2L, "x")).ToList();

    var batches = TranscriptBatches.ToJson("claude-code", Main, null, lines).ToList();

    batches.Count.ShouldBe(2);
    JsonDocument.Parse(batches[1]).RootElement.GetProperty("lines").GetArrayLength().ShouldBe(1);
  }

  [Fact]
  public void SplitsAtTheSizeLimitButNeverSplitsALine()
  {
    var big = new string('x', TranscriptBatches.MaxTextBytes / 2 + 1);
    var lines = new[] { new TranscriptLineRead(0, big), new TranscriptLineRead(1_000_000, big), new TranscriptLineRead(2_000_000, "small") };

    var batches = TranscriptBatches.ToJson("claude-code", Main, null, lines).ToList();

    batches.Select(b => JsonDocument.Parse(b).RootElement.GetProperty("lines").GetArrayLength()).ShouldBe([1, 2]);
  }
}
