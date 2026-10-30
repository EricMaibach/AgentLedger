using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentLedger.FunctionalTests.ApiEndpoints;
using AgentLedger.FunctionalTests.Authentication;

namespace AgentLedger.FunctionalTests.Enrollment;

// /settings/machines (ADR 0024): connect a machine with a one-time code, see connected machines, disconnect one.
public sealed partial class MachinesPageTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private const string Page = "/settings/machines";

  [Fact]
  public async Task OffersToConnectAMachine()
  {
    var browser = await factory.CreateSignedInBrowserAsync("connector");

    var html = await (await browser.GetAsync(Page)).Content.ReadAsStringAsync();

    html.ShouldContain("<title>Machines");
    html.ShouldContain("Connect a machine");
    html.ShouldContain("http://host.docker.internal"); // the dev container address choice
    html.ShouldContain("href=\"/settings/api-keys\""); // settings navigation
  }

  [Fact]
  public async Task ShowsTheCommandsWithACodeThatWorksOnce()
  {
    var browser = await factory.CreateSignedInBrowserAsync("connector");

    var response = await ConnectAsync(browser, "http://ledger.lan:58090");

    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    html.ShouldContain("curl -fsSL http://ledger.lan:58090/install.sh | sh -s -- --code ");
    html.ShouldContain("irm http://ledger.lan:58090/install.ps1 | iex");
    var code = CodePattern().Match(html).Groups["code"].Value;
    code.Length.ShouldBe(9);

    (await EnrollAsync(code, "laptop")).StatusCode.ShouldBe(HttpStatusCode.OK);
    (await EnrollAsync(code, "laptop")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await (await browser.GetAsync(Page)).Content.ReadAsStringAsync()).ShouldNotContain(code); // shown once
  }

  [Fact]
  public async Task RefusesAnAddressThatIsNotAWebAddress()
  {
    var browser = await factory.CreateSignedInBrowserAsync("connector");

    var html = await (await ConnectAsync(browser, "ftp://ledger.lan")).Content.ReadAsStringAsync();

    html.ShouldContain("role=\"alert\"");
    CodePattern().IsMatch(WebUtility.HtmlDecode(html)).ShouldBeFalse();
  }

  [Fact]
  public async Task ListsConnectedMachinesAndDisconnectsThem()
  {
    var browser = await factory.CreateSignedInBrowserAsync("disconnector");
    var code = CodePattern().Match(WebUtility.HtmlDecode(
      await (await ConnectAsync(browser, "http://ledger.lan:58090")).Content.ReadAsStringAsync())).Groups["code"].Value;
    var enrolled = JsonDocument.Parse(await (await EnrollAsync(code, "build-server")).Content.ReadAsStringAsync()).RootElement;
    var key = enrolled.GetProperty("apiKey").GetString();
    var keyId = enrolled.GetProperty("keyId").GetGuid();

    var listed = await (await browser.GetAsync(Page)).Content.ReadAsStringAsync();
    listed.ShouldContain("build-server");

    var response = await browser.SubmitFormAsync(Page, [], formName: $"disconnect-{keyId}");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await response.Content.ReadAsStringAsync()).ShouldContain("Disconnected");
    (await factory.CreateClientWithKey(key).PostAsync("/events", new StringContent(TestEnvelope.Json(), Encoding.UTF8, "application/json")))
      .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task ShowsTheCliVersionAMachineSends()
  {
    var browser = await factory.CreateSignedInBrowserAsync("versioner");
    var code = CodePattern().Match(WebUtility.HtmlDecode(
      await (await ConnectAsync(browser, "http://ledger.lan:58090")).Content.ReadAsStringAsync())).Groups["code"].Value;
    var key = JsonDocument.Parse(await (await EnrollAsync(code, "versioned-box")).Content.ReadAsStringAsync()).RootElement
      .GetProperty("apiKey").GetString();

    var cli = factory.CreateClientWithKey(key);
    cli.DefaultRequestHeaders.UserAgent.ParseAdd("agentledger-cli/0.0.1");
    (await cli.PostAsync("/events", new StringContent(TestEnvelope.Json(), Encoding.UTF8, "application/json"))).StatusCode
      .ShouldBe(HttpStatusCode.Created);

    var html = await (await browser.GetAsync(Page)).Content.ReadAsStringAsync();
    html.ShouldContain(">0.0.1</code>"); // the element carries a scoped-CSS attribute
    html.ShouldContain("updates itself to"); // the test server's version differs
  }

  [Fact]
  public async Task NeedsSignIn()
  {
    var response = await factory.CreateBrowser().GetAsync(Page);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.RedirectTarget().ShouldStartWith("/login");
  }

  private static Task<HttpResponseMessage> ConnectAsync(HttpClient browser, string address) =>
    browser.SubmitFormAsync(Page, new() { ["Connect.Address"] = "other", ["Connect.OtherAddress"] = address }, formName: "connect");

  private Task<HttpResponseMessage> EnrollAsync(string code, string host) =>
    factory.CreateClient().PostAsJsonAsync("/enroll", new { code, host });

  [GeneratedRegex("--code (?<code>[A-Z0-9]{4}-[A-Z0-9]{4})")]
  private static partial Regex CodePattern();
}
