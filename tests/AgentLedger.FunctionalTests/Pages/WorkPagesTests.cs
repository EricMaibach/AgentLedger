using System.Net;
using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using AgentLedger.FunctionalTests.Authentication;
using AgentLedger.UseCases.Projections;

namespace AgentLedger.FunctionalTests.Pages;

// The work pages (ADR 0023), requested as a browser would: the first response is complete HTML (prerendered), so the
// selection in the URL decides what it shows. Projections are stored directly; the worker never touches them (they
// have no raw rows and are at the current version).
public sealed class WorkPagesTests(CustomWebApplicationFactory<Program> factory)
  : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
  private static readonly AgentKind Agent = AgentKind.ClaudeCode;
  private static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
  private readonly string _session = $"session-{Guid.NewGuid():N}";
  private HttpClient _browser = default!;
  private ProjectedSession _projected = default!;

  public async Task InitializeAsync()
  {
    _browser = await factory.CreateSignedInBrowserAsync();
    _projected = Session();
    using var scope = factory.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IProjectionStore>()
      .ReplaceSessionAsync(_projected, ProjectionVersion.Current, CancellationToken.None);
  }

  public Task DisposeAsync() => Task.CompletedTask;

  private Guid SessionId => _projected.Id.Value;
  private Turn Turn => _projected.Turns[0];

  [Fact]
  public async Task TheHomePageGoesToWork()
  {
    var response = await _browser.GetAsync("/");

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.Headers.Location!.OriginalString.ShouldBe("/work");
  }

  [Fact]
  public async Task NeedsSignIn()
  {
    var response = await factory.CreateBrowser().GetAsync($"/work?unit={SessionId}");

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.RedirectTarget().ShouldStartWith("/login");
  }

  [Fact]
  public async Task ListsSessionsWithLinks()
  {
    var html = await GetPageAsync("/work");

    html.ShouldContain("<title>Work");
    html.ShouldContain($"href=\"/work?unit={SessionId}\"");
    html.ShouldContain("Fix the cart total");
  }

  [Fact]
  public async Task ShowsTheSelectedSessionsSummaryAndTurns()
  {
    var html = await GetPageAsync($"/work?unit={SessionId}");

    html.ShouldContain("<title>Fix the cart total");
    html.ShouldContain("Make the cart total include tax"); // the turn's prompt summary
    html.ShouldContain("Completed");
    html.ShouldContain($"href=\"/work?unit={SessionId}&amp;turn={Turn.Id.Value}\"");
  }

  [Fact]
  public async Task ShowsTheExpandedTurnsSteps()
  {
    var html = await GetPageAsync($"/work?unit={SessionId}&turn={Turn.Id.Value}");

    html.ShouldContain("dotnet test"); // the tool call's summary
    html.ShouldContain("summary not available"); // thinking without text
    html.ShouldContain($"href=\"/work?unit={SessionId}&amp;turn={Turn.Id.Value}&amp;step={Turn.Steps[2].Id.Value}\"");
  }

  [Fact]
  public async Task ShowsTheSelectedStepsDetails()
  {
    var html = await GetPageAsync($"/work?unit={SessionId}&turn={Turn.Id.Value}&step={Turn.Steps[2].Id.Value}");

    html.ShouldContain("Passed: 12"); // the tool's output
    html.ShouldContain("Evidence");
  }

  [Fact]
  public async Task RendersResponsesAsMarkdown()
  {
    var html = await GetPageAsync($"/work?unit={SessionId}&turn={Turn.Id.Value}&step={Turn.Steps[3].Id.Value}");

    html.ShouldContain("<strong>tax</strong>");
  }

  [Fact]
  public async Task AnUnknownSessionIsNotFound()
  {
    await GetPageAsync($"/work?unit={Guid.NewGuid()}", HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task ShortLinksGoToTheirPlace()
  {
    var turn = await _browser.GetAsync($"/work/turns/{Turn.Id.Value}");
    var step = await _browser.GetAsync($"/work/steps/{Turn.Steps[2].Id.Value}");

    turn.RedirectTarget().ShouldBe($"/work?unit={SessionId}&turn={Turn.Id.Value}");
    step.RedirectTarget().ShouldBe($"/work?unit={SessionId}&turn={Turn.Id.Value}&step={Turn.Steps[2].Id.Value}");
    (await _browser.GetAsync($"/work/turns/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Theory]
  [InlineData("/Components/Pages/Work/WorkPage.razor.js")] // the page imports this path
  [InlineData("/_framework/blazor.web.js")]
  public async Task ServesThePagesScripts(string path)
  {
    var response = await _browser.GetAsync(path);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("text/javascript");
  }

  [Fact]
  public async Task LoadsBlazorWithEnhancedNavigationOff()
  {
    // Static pages must keep behaving as plain HTML pages (ADR 0023).
    var html = await GetPageAsync("/raw/events");

    html.ShouldContain("disableDomPreservation: true");
  }

  [Fact]
  public async Task SignedOutPagesDontLoadBlazor()
  {
    // Signed out, the script would be refused (deny by default) and the login page's HTML returned in its place.
    var html = await (await factory.CreateBrowser().GetAsync("/login")).Content.ReadAsStringAsync();

    html.ShouldNotContain("blazor.web");
    html.ShouldNotContain("Blazor.start");
  }

  private async Task<string> GetPageAsync(string url, HttpStatusCode expected = HttpStatusCode.OK)
  {
    var response = await _browser.GetAsync(url);
    var html = await response.Content.ReadAsStringAsync();
    response.StatusCode.ShouldBe(expected, html.Length > 2000 ? html[..2000] : html);
    return html;
  }

  private ProjectedSession Session()
  {
    var responseId = ModelResponseId.For(Agent, _session, "msg_1");
    var lineage = new Lineage([AgentEventId.From(Guid.CreateVersion7())], []);
    Step[] steps =
    [
      Step.Prompt(StepId.For(Agent, _session, StepKind.Prompt, "p-1"), T0, "Make the cart total include tax", lineage),
      Step.Thinking(StepId.For(Agent, _session, StepKind.Thinking, "t"), T0.AddSeconds(1), null, responseId, lineage),
      Step.ToolCall(StepId.For(Agent, _session, StepKind.ToolCall, "toolu_1"), T0.AddSeconds(2), "Bash", "toolu_1",
        """{"command":"dotnet test"}""", "Passed: 12", true, T0.AddSeconds(9), responseId, lineage, "dotnet test"),
      Step.Response(StepId.For(Agent, _session, StepKind.Response, "r"), T0.AddSeconds(10), "The total now includes **tax**.",
        responseId, lineage),
    ];
    var response = new ModelResponse(responseId, "msg_1", "claude-opus-5-5", T0.AddSeconds(1), new TokenUsage(3, 400, 0, 50, 4000, 10, 0, 0),
      lineage);
    var turn = new Turn(TurnId.For(Agent, _session, "p-1"), "p-1", PromptSource.Typed, TurnStatus.Completed, T0, T0.AddSeconds(11),
      new TurnContext("github.com/acme/shop", "main", null, "/w", "/w", "devbox", "tester"), steps, [response]);
    return new ProjectedSession(Agent, _session, T0, T0.AddSeconds(11), [turn], "Fix the cart total");
  }
}
