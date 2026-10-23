using System.Net;
using AgentLedger.Cli.Login;

namespace AgentLedger.Cli.Tests.Login;

// Asks the ledger what a key is (GET /api/keys/current, ADR 0019), for `login` and `status`.
public sealed class ApiKeyCheckCheck
{
  private static readonly Uri ApiUrl = new("http://api.test:5000/");
  private const string Key = "agl_ingest_abc_def";

  [Fact]
  public async Task AsksTheCurrentKeyEndpointWithTheKey()
  {
    var http = StubHttp.Returning(HttpStatusCode.OK, """{"keyId":"x","name":"laptop","scope":"ingest","owner":"eric"}""");

    await ApiKeyCheck.CheckAsync(http.Client, ApiUrl, Key);

    http.Request!.RequestUri.ShouldBe(new Uri("http://api.test:5000/api/keys/current"));
    http.Request.Headers.Authorization!.ToString().ShouldBe($"Bearer {Key}");
  }

  [Fact]
  public async Task DescribesAnAcceptedKey()
  {
    var http = StubHttp.Returning(HttpStatusCode.OK, """{"keyId":"x","name":"laptop","scope":"ingest","owner":"eric"}""");

    var check = await ApiKeyCheck.CheckAsync(http.Client, ApiUrl, Key);

    check.ShouldBe(new ApiKeyCheck(ApiKeyCheckResult.Accepted, "accepted: 'laptop' (ingest, owner eric)", "laptop", "ingest", "eric"));
  }

  [Fact]
  public async Task ReportsARejectedKey()
  {
    var check = await ApiKeyCheck.CheckAsync(StubHttp.Returning(HttpStatusCode.Unauthorized).Client, ApiUrl, Key);

    check.Result.ShouldBe(ApiKeyCheckResult.Rejected);
    check.Detail.ShouldBe("rejected: unknown, revoked or expired");
  }

  [Fact]
  public async Task CannotTellWhenTheLedgerFails()
  {
    var check = await ApiKeyCheck.CheckAsync(StubHttp.Returning(HttpStatusCode.InternalServerError).Client, ApiUrl, Key);

    check.Result.ShouldBe(ApiKeyCheckResult.Unknown);
    check.Detail.ShouldContain("500");
  }

  [Fact]
  public async Task CannotTellWhenTheLedgerIsUnreachable()
  {
    var check = await ApiKeyCheck.CheckAsync(StubHttp.Unreachable().Client, ApiUrl, Key);

    check.Result.ShouldBe(ApiKeyCheckResult.Unknown);
    check.Detail.ShouldContain("Connection refused");
  }
}
