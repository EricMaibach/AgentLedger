using System.Net;
using AgentLedger.Web.Users;

namespace AgentLedger.FunctionalTests.Authentication;

// `dotnet AgentLedger.Web.dll reset-password <username>`, run inside the container (ADR 0018).
public sealed class ResetPasswordCommandTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  [Fact]
  public async Task PrintsANewPasswordThatWorksAndTheOldOneDoesNot()
  {
    await factory.EnsureUserAsync("forgetful");
    var output = new StringWriter();

    var exitCode = await ResetPasswordCommandLine.RunAsync(factory.Services, ["reset-password", "forgetful"], output);

    exitCode.ShouldBe(0);
    var newPassword = output.ToString().Split('\n').Select(l => l.Trim()).Single(l => l.Length == 24);
    (await factory.CreateBrowser().SignInAsync("forgetful", newPassword)).StatusCode.ShouldBe(HttpStatusCode.Redirect);
    (await factory.CreateBrowser().SignInAsync("forgetful", Browser.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Fact]
  public async Task FailsForAnUnknownUser()
  {
    var output = new StringWriter();

    var exitCode = await ResetPasswordCommandLine.RunAsync(factory.Services, ["reset-password", "nobody"], output);

    exitCode.ShouldBe(1);
    output.ToString().ShouldContain("No user named 'nobody'");
  }

  [Fact]
  public async Task ExplainsUsageWithoutAUsername()
  {
    var output = new StringWriter();

    var exitCode = await ResetPasswordCommandLine.RunAsync(factory.Services, ["reset-password"], output);

    exitCode.ShouldBe(2);
    output.ToString().ShouldContain("Usage: reset-password <username>");
  }

  [Fact]
  public void RecognizesOnlyItsOwnCommand()
  {
    ResetPasswordCommandLine.IsInvokedBy(["reset-password", "eric"]).ShouldBeTrue();
    ResetPasswordCommandLine.IsInvokedBy([]).ShouldBeFalse();
    ResetPasswordCommandLine.IsInvokedBy(["--urls=http://+:8080"]).ShouldBeFalse();
  }
}
