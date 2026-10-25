using System.Net;

namespace AgentLedger.FunctionalTests.Authentication;

// Deny by default (ADR 0018): everything needs a signed-in user except what is explicitly open.
public sealed class AccessTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  [Theory]
  [InlineData("/raw/events")]
  [InlineData("/raw/events/0199a0b0-0000-7000-8000-000000000001")]
  [InlineData("/account/password")]
  [InlineData("/settings/api-keys")]
  [InlineData("/raw/sessions/s1/transcript")]
  [InlineData("/")]
  public async Task PagesSendSignedOutVisitorsToLoginAndBack(string url)
  {
    await factory.EnsureUserAsync("tester"); // with no users, login would go on to setup

    var response = await factory.CreateBrowser().GetAsync(url);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.RedirectTarget().ShouldBe($"/login?returnUrl={Uri.EscapeDataString(url)}");
  }

  [Theory]
  [InlineData("/api/raw/events")]
  [InlineData("/api/raw/events/counts")]
  [InlineData("/api/raw/transcripts?session=s1")]
  [InlineData("/api/raw/transcripts/lines?session=s1")]
  [InlineData("/api/raw/events/0199a0b0-0000-7000-8000-000000000001")]
  public async Task TheApiAnswers401NotALoginPage(string url)
  {
    var response = await factory.CreateBrowser().GetAsync(url);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Theory]
  [InlineData("/status")]
  [InlineData("/app.css")]
  [InlineData("/app.js")]
  public async Task StatusAndStaticFilesStayOpen(string url)
  {
    (await factory.CreateBrowser().GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Fact]
  public async Task TheLoginPageIsOpen()
  {
    await factory.EnsureUserAsync("tester");

    var response = await factory.CreateBrowser().GetAsync("/login");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await response.Content.ReadAsStringAsync()).ShouldContain("<title>Sign in");
  }

  [Fact]
  public async Task ASignedInBrowserCanUseThePagesAndTheApi()
  {
    var browser = await factory.CreateSignedInBrowserAsync();

    (await browser.GetAsync("/raw/events")).StatusCode.ShouldBe(HttpStatusCode.OK);
    (await browser.GetAsync("/api/raw/events")).StatusCode.ShouldBe(HttpStatusCode.OK);
  }
}
