using System.Net;

namespace AgentLedger.FunctionalTests.Authentication;

// /account/password (ADR 0018). Each test uses its own user, since changing a password affects every session.
public sealed class ChangePasswordTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private const string NewPassword = "a completely different passphrase";

  private readonly string _username = $"user-{Guid.NewGuid():N}"[..20];

  private static Task<HttpResponseMessage> ChangeAsync(HttpClient browser, string current, string @new, string? confirm = null) =>
    browser.SubmitFormAsync("/account/password", new()
    {
      ["Input.CurrentPassword"] = current,
      ["Input.NewPassword"] = @new,
      ["Input.ConfirmPassword"] = confirm ?? @new,
    });

  [Fact]
  public async Task ChangesThePassword()
  {
    var browser = await factory.CreateSignedInBrowserAsync(_username);

    var response = await ChangeAsync(browser, Browser.Password, NewPassword);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await response.Content.ReadAsStringAsync()).ShouldContain("Password changed");
    (await factory.CreateBrowser().SignInAsync(_username, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
    (await factory.CreateBrowser().SignInAsync(_username, Browser.Password)).StatusCode.ShouldBe(HttpStatusCode.OK); // old one rejected
  }

  [Fact]
  public async Task KeepsThisSessionButEndsTheOthers()
  {
    var here = await factory.CreateSignedInBrowserAsync(_username);
    var elsewhere = await factory.CreateSignedInBrowserAsync(_username);

    await ChangeAsync(here, Browser.Password, NewPassword);

    (await here.GetAsync("/raw/events")).StatusCode.ShouldBe(HttpStatusCode.OK);
    (await elsewhere.GetAsync("/raw/events")).StatusCode.ShouldBe(HttpStatusCode.Redirect);
  }

  [Fact]
  public async Task RejectsAWrongCurrentPassword()
  {
    var browser = await factory.CreateSignedInBrowserAsync(_username);

    var response = await ChangeAsync(browser, "not the current password", NewPassword);

    (await response.Content.ReadAsStringAsync()).ShouldContain("Current password is incorrect.");
  }

  [Fact]
  public async Task RejectsAShortNewPassword()
  {
    var browser = await factory.CreateSignedInBrowserAsync(_username);

    var response = await ChangeAsync(browser, Browser.Password, "short");

    (await response.Content.ReadAsStringAsync()).ShouldContain("15 to 128 characters");
  }

  [Fact]
  public async Task RejectsAConfirmationThatDoesNotMatch()
  {
    var browser = await factory.CreateSignedInBrowserAsync(_username);

    var response = await ChangeAsync(browser, Browser.Password, NewPassword, confirm: NewPassword + "x");

    (await response.Content.ReadAsStringAsync()).ShouldContain("don&#x27;t match");
    (await factory.CreateBrowser().SignInAsync(_username, Browser.Password)).StatusCode.ShouldBe(HttpStatusCode.Redirect); // unchanged
  }
}
