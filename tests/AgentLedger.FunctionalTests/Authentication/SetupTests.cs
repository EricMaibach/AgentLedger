using System.Net;

namespace AgentLedger.FunctionalTests.Authentication;

// First-run setup (ADR 0018), on a fresh database with no users. One journey, in order, because setup
// only works once per database.
public sealed class SetupTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  [Fact]
  public async Task CreatesTheFirstAdminOnceAndThenDisappears()
  {
    var browser = factory.CreateBrowser();

    // With no users, the login page sends you to setup.
    var login = await browser.GetAsync("/login");
    login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    login.RedirectTarget().ShouldBe("/setup");

    // The password rules apply.
    var tooShort = await browser.SubmitFormAsync("/setup", new() { ["Input.Username"] = "eric", ["Input.Password"] = "short" });
    tooShort.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await tooShort.Content.ReadAsStringAsync()).ShouldContain("15 to 128 characters");

    // Setting up signs you in.
    var setUp = await browser.SubmitFormAsync("/setup", new() { ["Input.Username"] = "eric", ["Input.Password"] = Browser.Password });
    setUp.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    setUp.RedirectTarget().ShouldBe("/raw/events");
    (await browser.GetAsync("/raw/events")).StatusCode.ShouldBe(HttpStatusCode.OK);

    // Afterwards setup is gone for good, and login is a normal page.
    (await factory.CreateBrowser().GetAsync("/setup")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    (await factory.CreateBrowser().GetAsync("/login")).StatusCode.ShouldBe(HttpStatusCode.OK);
  }
}

// The setup window closes 10 minutes after startup (ADR 0018).
public sealed class SetupTimeoutTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly CustomWebApplicationFactory<Program> _factory;

  public SetupTimeoutTests(CustomWebApplicationFactory<Program> factory)
  {
    _factory = factory;
    _factory.UseFakeClock();
  }

  [Fact]
  public async Task ExplainsHowToReopenSetupAfterTheWindowCloses()
  {
    var browser = _factory.CreateBrowser();
    (await browser.GetAsync("/setup")).StatusCode.ShouldBe(HttpStatusCode.OK); // starts the app and its window

    _factory.Clock!.Advance(TimeSpan.FromMinutes(10));

    var page = await browser.GetAsync("/setup");
    page.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await page.Content.ReadAsStringAsync()).ShouldContain("Restart");
  }
}
