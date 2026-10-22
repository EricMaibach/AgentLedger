using System.Net;
using System.Text.RegularExpressions;
using AgentLedger.Core.UserAggregate;
using AgentLedger.Core.UserAggregate.Specifications;
using AgentLedger.UseCases.Users;
using Ardalis.SharedKernel;

namespace AgentLedger.FunctionalTests.Authentication;

// Drives the server-rendered forms as a browser would: load the page, keep its hidden fields (the antiforgery
// token and Blazor's form name), fill in the visible ones and post. Cookies are kept by the client.
internal static partial class Browser
{
  public const string Password = "correct horse battery staple";

  public static HttpClient CreateBrowser(this CustomWebApplicationFactory<Program> factory) =>
    factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  public static async Task<HttpClient> CreateSignedInBrowserAsync(this CustomWebApplicationFactory<Program> factory,
    string username = "tester")
  {
    await factory.EnsureUserAsync(username);
    var browser = factory.CreateBrowser();
    var response = await browser.SignInAsync(username, Password);
    response.StatusCode.ShouldBe(HttpStatusCode.Redirect, "signing in should redirect");
    return browser;
  }

  // Where a redirect goes, as a local path and query: Blazor redirects to absolute URLs on the same host.
  public static string RedirectTarget(this HttpResponseMessage response)
  {
    var location = response.Headers.Location.ShouldNotBeNull("expected a redirect");
    if (!location.IsAbsoluteUri)
    {
      return location.OriginalString;
    }

    location.Host.ShouldBe("localhost", "redirects must stay on this site");
    return location.PathAndQuery;
  }

  public static Task<HttpResponseMessage> SignInAsync(this HttpClient browser, string username, string password, string url = "/login") =>
    browser.SubmitFormAsync(url, new() { ["Input.Username"] = username, ["Input.Password"] = password });

  // Creates the user directly, so tests don't depend on the setup page (which only works once per database).
  public static async Task EnsureUserAsync(this CustomWebApplicationFactory<Program> factory, string username, string password = Password)
  {
    using var scope = factory.Services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
    if (await repository.FirstOrDefaultAsync(new UserByUsernameSpec(Username.From(username))) is null)
    {
      var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
      await repository.AddAsync(new User(UserId.From(Guid.CreateVersion7()), Username.From(username), hasher.Hash(password),
        SecurityStamp.From(Guid.CreateVersion7()), isAdmin: true));
    }
  }

  public static async Task<HttpResponseMessage> SubmitFormAsync(this HttpClient browser, string url, Dictionary<string, string> fields,
    string? formAction = null)
  {
    var page = await browser.GetAsync(url);
    page.StatusCode.ShouldBe(HttpStatusCode.OK, $"the form page {url} should load");
    var html = await page.Content.ReadAsStringAsync();

    var values = HiddenFields(html, formAction);
    foreach (var (name, value) in fields)
    {
      values[name] = value;
    }

    return await browser.PostAsync(formAction ?? url, new FormUrlEncodedContent(values));
  }

  // The hidden inputs of the form posting to formAction, or else of the page's own Blazor form (the one
  // carrying a form name; the header's sign-out form has none).
  private static Dictionary<string, string> HiddenFields(string html, string? formAction)
  {
    var form = FormPattern().Matches(html)
      .FirstOrDefault(f => formAction is null
        ? f.Value.Contains("name=\"_handler\"", StringComparison.Ordinal)
        : f.Groups["open"].Value.Contains($"action=\"{formAction}\"", StringComparison.Ordinal))
      ?? throw new InvalidOperationException($"No form{(formAction is null ? "" : $" posting to {formAction}")} on the page.");

    return HiddenInputPattern().Matches(form.Value)
      .Select(input => (Name: Attribute(input.Value, "name"), Value: Attribute(input.Value, "value")))
      .Where(field => field.Name is not null)
      .ToDictionary(field => field.Name!, field => WebUtility.HtmlDecode(field.Value ?? ""));
  }

  private static string? Attribute(string tag, string name)
  {
    var match = Regex.Match(tag, $"\\b{name}=\"(?<value>[^\"]*)\"");
    return match.Success ? match.Groups["value"].Value : null;
  }

  [GeneratedRegex("(?<open><form[^>]*>).*?</form>", RegexOptions.Singleline)]
  private static partial Regex FormPattern();

  [GeneratedRegex("<input[^>]*type=\"hidden\"[^>]*>")]
  private static partial Regex HiddenInputPattern();
}
