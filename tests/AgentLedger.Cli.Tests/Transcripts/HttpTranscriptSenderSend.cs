using System.IO.Compression;
using System.Net;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Tests.Login;
using AgentLedger.Cli.Transcripts;

namespace AgentLedger.Cli.Tests.Transcripts;

// POSTs a batch to /transcripts/lines, gzipped (ADR 0020), with the same status rules as events.
public sealed class HttpTranscriptSenderSend
{
  private static readonly Uri ApiUrl = new("http://api.test:5000");

  [Fact]
  public async Task PostsTheBatchGzippedWithTheKey()
  {
    byte[]? body = null;
    HttpRequestMessage? request = null;
    var http = new StubHttp(r =>
    {
      request = r;
      body = r.Content!.ReadAsByteArrayAsync().Result;
      return new HttpResponseMessage(HttpStatusCode.OK);
    });

    var outcome = await new HttpTranscriptSender(http.Client, ApiUrl, "agl_ingest_x_y").SendAsync("""{"lines":[]}""", CancellationToken.None);

    outcome.Result.ShouldBe(SendResult.Accepted);
    request!.RequestUri.ShouldBe(new Uri("http://api.test:5000/transcripts/lines"));
    request.Headers.Authorization!.ToString().ShouldBe("Bearer agl_ingest_x_y");
    request.Content!.Headers.ContentEncoding.ShouldBe(["gzip"]);
    using var unzipped = new StreamReader(new GZipStream(new MemoryStream(body!), CompressionMode.Decompress));
    (await unzipped.ReadToEndAsync()).ShouldBe("""{"lines":[]}""");
  }

  [Theory]
  [InlineData(HttpStatusCode.BadRequest, (int)SendResult.Rejected)]
  [InlineData(HttpStatusCode.RequestEntityTooLarge, (int)SendResult.Rejected)] // too big: resending won't help
  [InlineData(HttpStatusCode.Unauthorized, (int)SendResult.Failed)]           // key problem: keep it (ADR 0019)
  [InlineData(HttpStatusCode.ServiceUnavailable, (int)SendResult.Failed)]
  public async Task MapsTheResponseStatusLikeEvents(HttpStatusCode status, int expected)
  {
    var outcome = await new HttpTranscriptSender(StubHttp.Returning(status).Client, ApiUrl, "k").SendAsync("{}", CancellationToken.None);

    outcome.Result.ShouldBe((SendResult)expected);
  }
}
