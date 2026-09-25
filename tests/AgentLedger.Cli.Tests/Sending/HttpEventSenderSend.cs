using System.Net;
using AgentLedger.Cli.Sending;

namespace AgentLedger.Cli.Tests.Sending;

public sealed class HttpEventSenderSend
{
  private static readonly Uri ApiUrl = new("http://api.test:5000");

  // Stands in for the API: returns whatever the test says, and records what it received.
  private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
  {
    public HttpRequestMessage? Request { get; private set; }
    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Request = request;
      Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
      return respond(request);
    }
  }

  private static (HttpEventSender Sender, StubHandler Handler) Create(HttpStatusCode status, string? apiKey = null)
  {
    var handler = new StubHandler(_ => new HttpResponseMessage(status) { Content = new StringContent("detail") });
    return (new HttpEventSender(new HttpClient(handler), ApiUrl, apiKey), handler);
  }

  [Fact]
  public async Task PostsTheEnvelopeAsJsonToTheEventsEndpoint()
  {
    var (sender, handler) = Create(HttpStatusCode.Created);

    await sender.SendAsync("""{"eventId":"x"}""", CancellationToken.None);

    handler.Request!.Method.ShouldBe(HttpMethod.Post);
    handler.Request.RequestUri.ShouldBe(new Uri("http://api.test:5000/events"));
    handler.Request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
    handler.Body.ShouldBe("""{"eventId":"x"}""");
  }

  [Theory]
  [InlineData(HttpStatusCode.Created, (int)SendResult.Accepted)]
  [InlineData(HttpStatusCode.BadRequest, (int)SendResult.Rejected)]            // malformed: resending won't help
  [InlineData(HttpStatusCode.InternalServerError, (int)SendResult.Failed)]     // server problem: keep and retry
  [InlineData(HttpStatusCode.ServiceUnavailable, (int)SendResult.Failed)]
  [InlineData(HttpStatusCode.RequestTimeout, (int)SendResult.Failed)]          // 4xx, but temporary
  [InlineData(HttpStatusCode.TooManyRequests, (int)SendResult.Failed)]         // 4xx, but temporary
  // `int`, not SendResult: a public test method can't take the CLI's internal types as parameters.
  public async Task MapsTheResponseStatus(HttpStatusCode status, int expected)
  {
    var (sender, _) = Create(status);

    (await sender.SendAsync("{}", CancellationToken.None)).Result.ShouldBe((SendResult)expected);
  }

  [Fact]
  public async Task TreatsAnUnreachableApiAsFailed()
  {
    var handler = new StubHandler(_ => throw new HttpRequestException("Connection refused"));
    var sender = new HttpEventSender(new HttpClient(handler), ApiUrl, apiKey: null);

    var outcome = await sender.SendAsync("{}", CancellationToken.None);

    outcome.Result.ShouldBe(SendResult.Failed);
    outcome.Detail.ShouldContain("Connection refused");
  }

  [Fact]
  public async Task TreatsATimeoutAsFailed()
  {
    var handler = new StubHandler(_ => throw new TaskCanceledException("timed out"));
    var sender = new HttpEventSender(new HttpClient(handler), ApiUrl, apiKey: null);

    (await sender.SendAsync("{}", CancellationToken.None)).Result.ShouldBe(SendResult.Failed);
  }

  [Fact]
  public async Task SendsTheApiKeyAsABearerTokenWhenConfigured()
  {
    var (sender, handler) = Create(HttpStatusCode.Created, apiKey: "secret");

    await sender.SendAsync("{}", CancellationToken.None);

    handler.Request!.Headers.Authorization!.ToString().ShouldBe("Bearer secret");
  }

  [Fact]
  public async Task SendsNoAuthorizationWithoutAnApiKey()
  {
    var (sender, handler) = Create(HttpStatusCode.Created);

    await sender.SendAsync("{}", CancellationToken.None);

    handler.Request!.Headers.Authorization.ShouldBeNull();
  }
}
