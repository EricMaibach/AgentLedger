using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AgentLedger.Cli.Transcripts;

/// <summary>
/// Lines become batches in the server's format for POST /transcripts/lines (ADR 0020): at most
/// <see cref="MaxLines"/> lines or <see cref="MaxTextBytes"/> of text each; a line is never split, and a line
/// bigger than the size limit goes alone.
/// </summary>
internal static class TranscriptBatches
{
  public const int MaxLines = 1000;
  public const int MaxTextBytes = 4 * 1024 * 1024;

  // Not for HTML: only the ledger reads this. Leaving non-ASCII unescaped keeps batches small.
  private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

  public static IEnumerable<string> ToJson(string agent, TranscriptFileRef file, string? meta, IReadOnlyList<TranscriptLineRead> lines)
  {
    var batch = new List<TranscriptLineRead>();
    var bytes = 0;
    foreach (var line in lines)
    {
      var size = Encoding.UTF8.GetByteCount(line.Text);
      if (batch.Count > 0 && (batch.Count == MaxLines || bytes + size > MaxTextBytes))
      {
        yield return Write(agent, file, meta, batch);
        batch = [];
        bytes = 0;
      }

      batch.Add(line);
      bytes += size;
    }

    if (batch.Count > 0)
    {
      yield return Write(agent, file, meta, batch);
    }
  }

  private static string Write(string agent, TranscriptFileRef file, string? meta, List<TranscriptLineRead> lines)
  {
    using var buffer = new MemoryStream();
    using (var writer = new Utf8JsonWriter(buffer, Options))
    {
      writer.WriteStartObject();
      writer.WriteString("agent", agent);
      writer.WriteString("nativeSessionId", file.SessionId);
      writer.WriteString("file", file.FileName);
      if (meta is null)
      {
        writer.WriteNull("subagentMeta");
      }
      else
      {
        writer.WriteString("subagentMeta", meta);
      }

      writer.WriteStartArray("lines");
      foreach (var line in lines)
      {
        writer.WriteStartObject();
        writer.WriteNumber("byteOffset", line.ByteOffset);
        writer.WriteString("text", line.Text);
        writer.WriteEndObject();
      }

      writer.WriteEndArray();
      writer.WriteEndObject();
    }

    return Encoding.UTF8.GetString(buffer.ToArray());
  }
}
