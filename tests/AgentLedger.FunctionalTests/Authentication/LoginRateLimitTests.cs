using System.Net;
using AgentLedger.Web.Users;

namespace AgentLedger.FunctionalTests.Authentication;

// At most 10 login attempts a minute per IP address (ADR 0018). Its own class: the limit is per app instance.
public sealed class LoginRateLimitTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private readonly CustomWebApplicationFactory<Program> _factory;

  public LoginRateLimitTests(CustomWebApplicationFactory<Program> factory)
  {
    _factory = factory;
    factory.UseSetting("Authentication:LoginAttemptsPerMinute", $"{AuthenticationConfig.DefaultLoginAttemptsPerMinute}");
  }

  [Fact]
  public async Task RefusesTheEleventhAttemptInAMinute()
  {
    await _factory.EnsureUserAsync("tester");
    var browser = _factory.CreateBrowser();

    for (var i = 0; i < 10; i++)
    {
      // Different usernames, so the per-user lockout isn't what stops them.
      (await browser.SignInAsync($"guess{i}", "wrong password, quite long")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    (await browser.SignInAsync("guess10", "wrong password, quite long")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    (await browser.GetAsync("/login")).StatusCode.ShouldBe(HttpStatusCode.OK); // only attempts are limited, not the page
  }
}
