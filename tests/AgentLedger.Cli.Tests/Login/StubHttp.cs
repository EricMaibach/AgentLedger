using System.Net;

namespace AgentLedger.Cli.Tests.Login;

// Stands in for the ledger: returns whatever the test says, and records the request.
internal sealed class StubHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
  public HttpRequestMessage? Request { get; private set; }

  public static StubHttp Returning(HttpStatusCode status, string body = "") =>
    new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

  public static StubHttp Unreachable() => new(_ => throw new HttpRequestException("Connection refused"));

  public HttpClient Client => new(this);

  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    Request = request;
    return Task.FromResult(respond(request));
  }
}
