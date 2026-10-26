using System.Net;
using AgentLedger.Cli.Git;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Tests.Login;

namespace AgentLedger.Cli.Tests.Git;

// POSTs a batch to /git/observations (ADR 0021), settled like events.
public sealed class HttpGitSenderSend
{
  [Fact]
  public async Task PostsTheBatchWithTheKey()
  {
    HttpRequestMessage? request = null;
    var http = new StubHttp(r => { request = r; return new HttpResponseMessage(HttpStatusCode.OK); });

    var outcome = await new HttpGitSender(http.Client, new Uri("http://api.test:5000"), "agl_ingest_x_y").SendAsync("{}", CancellationToken.None);

    outcome.Result.ShouldBe(SendResult.Accepted);
    request!.RequestUri.ShouldBe(new Uri("http://api.test:5000/git/observations"));
    request.Headers.Authorization!.ToString().ShouldBe("Bearer agl_ingest_x_y");
  }

  [Theory]
  [InlineData(HttpStatusCode.BadRequest, (int)SendResult.Rejected)]
  [InlineData(HttpStatusCode.Unauthorized, (int)SendResult.Failed)]
  [InlineData(HttpStatusCode.NotFound, (int)SendResult.Rejected)]
  public async Task MapsTheResponseStatusLikeEvents(HttpStatusCode status, int expected)
  {
    var outcome = await new HttpGitSender(StubHttp.Returning(status).Client, new Uri("http://api.test:5000"), "k").SendAsync("{}", CancellationToken.None);

    outcome.Result.ShouldBe((SendResult)expected);
  }
}
