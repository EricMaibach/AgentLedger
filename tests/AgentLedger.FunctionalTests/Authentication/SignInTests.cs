using System.Net;

namespace AgentLedger.FunctionalTests.Authentication;

// The login page, sign-out and the signed-in header (ADR 0018).
public sealed class SignInTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  [Fact]
  public async Task SigningInGoesBackToThePageAskedFor()
  {
    await factory.EnsureUserAsync("tester");
    var browser = factory.CreateBrowser();

    var response = await browser.SignInAsync("Tester", Browser.Password, "/login?returnUrl=%2Fraw%2Fevents%3Fdistinct%3Dfalse");

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.RedirectTarget().ShouldBe("/raw/events?distinct=false");
    (await browser.GetAsync("/raw/events")).StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Theory]
  [InlineData("https://evil.example/")]
  [InlineData("//evil.example/")]
  [InlineData("/\\evil.example/")]
  public async Task NeverRedirectsToAnotherSite(string returnUrl)
  {
    await factory.EnsureUserAsync("tester");

    var response = await factory.CreateBrowser().SignInAsync("tester", Browser.Password, $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.RedirectTarget().ShouldBe("/raw/events");
  }

  [Theory]
  [InlineData("tester", "not the right password")]
  [InlineData("nobody", Browser.Password)]
  public async Task AWrongUsernameOrPasswordGetsTheSameMessage(string username, string password)
  {
    await factory.EnsureUserAsync("tester");
    var browser = factory.CreateBrowser();

    var response = await browser.SignInAsync(username, password);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await response.Content.ReadAsStringAsync()).ShouldContain("Username or password is incorrect.");
    (await browser.GetAsync("/raw/events")).StatusCode.ShouldBe(HttpStatusCode.Redirect); // still signed out
  }

  [Fact]
  public async Task TheHeaderShowsWhoIsSignedInAndSignOut()
  {
    var browser = await factory.CreateSignedInBrowserAsync("header-user");

    var html = await (await browser.GetAsync("/raw/events")).Content.ReadAsStringAsync();

    html.ShouldContain("header-user");
    html.ShouldContain("Sign out");
  }

  [Fact]
  public async Task SigningOutEndsTheSession()
  {
    var browser = await factory.CreateSignedInBrowserAsync();

    var response = await browser.SubmitFormAsync("/raw/events", [], formAction: "/logout");

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.RedirectTarget().ShouldBe("/login");
    (await browser.GetAsync("/raw/events")).StatusCode.ShouldBe(HttpStatusCode.Redirect);
  }

  [Fact]
  public async Task SigningOutNeedsTheAntiforgeryToken()
  {
    // Another site can't sign you out with a hidden form.
    var browser = await factory.CreateSignedInBrowserAsync();

    var response = await browser.PostAsync("/logout", new FormUrlEncodedContent([]));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await browser.GetAsync("/raw/events")).StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Fact]
  public async Task ASignedInVisitorToLoginGoesStraightOn()
  {
    var browser = await factory.CreateSignedInBrowserAsync();

    var response = await browser.GetAsync("/login");

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.RedirectTarget().ShouldBe("/raw/events");
  }
}
