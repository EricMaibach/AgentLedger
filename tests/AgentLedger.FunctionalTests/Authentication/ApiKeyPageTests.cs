using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.FunctionalTests.ApiEndpoints;

namespace AgentLedger.FunctionalTests.Authentication;

// /settings/api-keys (ADR 0019): create a key (shown once), see and revoke keys.
public sealed partial class ApiKeyPageTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  private const string Page = "/settings/api-keys";

  private static StringContent Envelope(Guid? eventId = null) =>
    new(TestEnvelope.Json(eventId: eventId), Encoding.UTF8, "application/json");

  [Fact]
  public async Task CreatesAKeyAndShowsItOnlyOnce()
  {
    var browser = await factory.CreateSignedInBrowserAsync("creator");

    var created = await browser.SubmitFormAsync(Page, new()
    {
      ["Input.Name"] = "laptop",
      ["Input.Scope"] = "ingest",
      ["Input.ExpiresInDays"] = "",
    }, formName: "create-key");

    created.StatusCode.ShouldBe(HttpStatusCode.OK);
    var token = TokenPattern().Match(await created.Content.ReadAsStringAsync()).Value;
    token.ShouldStartWith("agl_ingest_");
    (await factory.CreateClientWithKey(token).PostAsync("/events", Envelope())).StatusCode.ShouldBe(HttpStatusCode.Created);

    var later = await (await browser.GetAsync(Page)).Content.ReadAsStringAsync();
    later.ShouldContain("laptop");
    later.ShouldNotContain(token);
  }

  [Fact]
  public async Task RejectsAKeyWithoutAName()
  {
    var browser = await factory.CreateSignedInBrowserAsync("creator");

    var response = await browser.SubmitFormAsync(Page, new()
    {
      ["Input.Name"] = " ",
      ["Input.Scope"] = "read",
      ["Input.ExpiresInDays"] = "",
    }, formName: "create-key");

    var html = await response.Content.ReadAsStringAsync();
    html.ShouldContain("role=\"alert\"");
    TokenPattern().IsMatch(html).ShouldBeFalse();
  }

  [Fact]
  public async Task RevokesAKeyAtOnce()
  {
    var browser = await factory.CreateSignedInBrowserAsync("revoker");
    var token = await factory.CreateApiKeyAsync("ingest", "revoker", "old laptop");
    ApiKeyToken.TryParse(token, out var parsed).ShouldBeTrue();

    var response = await browser.SubmitFormAsync(Page, [], formName: $"revoke-{parsed.KeyId.Value}");

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await response.Content.ReadAsStringAsync()).ShouldContain("Revoked");
    (await factory.CreateClientWithKey(token).PostAsync("/events", Envelope())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task MembersSeeOnlyTheirOwnKeys()
  {
    await factory.CreateApiKeyAsync("ingest", "someone-else", "someone else's key");
    await factory.EnsureUserAsync("member", isAdmin: false);
    await factory.CreateApiKeyAsync("ingest", "member", "member's key");
    var member = await factory.CreateSignedInBrowserAsync("member", isAdmin: false);

    var html = await (await member.GetAsync(Page)).Content.ReadAsStringAsync();

    html.ShouldContain("member&#x27;s key");
    html.ShouldNotContain("someone else&#x27;s key");
  }

  [Fact]
  public async Task AdminsSeeEveryonesKeys()
  {
    await factory.CreateApiKeyAsync("ingest", "another-user", "another user's key");
    var admin = await factory.CreateSignedInBrowserAsync("admin-user", isAdmin: true);

    var html = await (await admin.GetAsync(Page)).Content.ReadAsStringAsync();

    html.ShouldContain("another user&#x27;s key");
  }

  [Fact]
  public async Task TheNavigationLeadsToSettings()
  {
    // The menu links to /settings (so it stays highlighted on every Settings page), which opens Machines (ADR 0024);
    // the Settings pages link to each other.
    var browser = await factory.CreateSignedInBrowserAsync();

    (await (await browser.GetAsync("/raw/events")).Content.ReadAsStringAsync()).ShouldContain("href=\"/settings\"");
    (await browser.GetAsync("/settings")).RedirectTarget().ShouldBe("/settings/machines");
    (await (await browser.GetAsync(Page)).Content.ReadAsStringAsync()).ShouldContain("href=\"/settings/machines\"");
  }

  [Fact]
  public async Task TheEventPageSaysWhoSentEachReceipt()
  {
    var ingest = factory.CreateClientWithKey(await factory.CreateApiKeyAsync("ingest", "sender", "dev container"));
    var eventId = Guid.CreateVersion7();
    await ingest.PostAsync("/events", Envelope(eventId));
    var browser = await factory.CreateSignedInBrowserAsync();

    var html = await (await browser.GetAsync($"/raw/events/{eventId}")).Content.ReadAsStringAsync();

    html.ShouldContain("Sent by");
    html.ShouldContain("sender");
    html.ShouldContain("dev container");
  }

  [GeneratedRegex("agl_(ingest|read)_[0-9A-Za-z]{22}_[0-9A-Za-z]{43}")]
  private static partial Regex TokenPattern();
}
