using System.Net;
using System.Net.Http.Json;
using AgentLedger.Web.Users;

namespace AgentLedger.FunctionalTests.Authentication;

// At most 10 login attempts a minute per IP address (ADR 0018), and separately 10 enrollment attempts (ADR 0024).
// Its own class: the limit is per app instance.
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

  [Fact]
  public async Task RefusesTheEleventhEnrollmentAttemptInAMinute()
  {
    // Guessing codes is as limited as guessing passwords.
    var client = _factory.CreateClient();
    for (var i = 0; i < 10; i++)
    {
      (await client.PostAsJsonAsync("/enroll", new { code = $"AAAA-AAA{i}", host = "guesser" })).StatusCode
        .ShouldBe(HttpStatusCode.BadRequest);
    }

    (await client.PostAsJsonAsync("/enroll", new { code = "AAAA-AAAB", host = "guesser" })).StatusCode
      .ShouldBe(HttpStatusCode.TooManyRequests);
  }
}
