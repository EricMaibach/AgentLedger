using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace AgentLedger.FunctionalTests.Transcripts;

// Transcript batches as the CLI sends them to POST /transcripts/lines (ADR 0020): one file's chunk, gzipped.
internal static class TranscriptBatches
{
  public static string Json(string session, IEnumerable<(long Offset, string Text)> lines, string file = "main",
    string? subagentMeta = null, string agent = "claude-code")
  {
    var batch = new JsonObject
    {
      ["agent"] = agent,
      ["nativeSessionId"] = session,
      ["file"] = file,
      ["subagentMeta"] = subagentMeta,
      ["lines"] = new JsonArray([.. lines.Select(l => (JsonNode)new JsonObject { ["byteOffset"] = l.Offset, ["text"] = l.Text })]),
    };
    return batch.ToJsonString();
  }

  public static HttpContent Gzipped(string json)
  {
    using var buffer = new MemoryStream();
    using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
    {
      gzip.Write(Encoding.UTF8.GetBytes(json));
    }

    var content = new ByteArrayContent(buffer.ToArray());
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    content.Headers.ContentEncoding.Add("gzip");
    return content;
  }

  public static HttpContent Plain(string json) => new StringContent(json, Encoding.UTF8, "application/json");

  public static Task<HttpResponseMessage> SendAsync(HttpClient client, string session, IEnumerable<(long, string)> lines,
    string file = "main", string? subagentMeta = null) =>
    client.PostAsync("/transcripts/lines", Gzipped(Json(session, lines, file, subagentMeta)));
}
