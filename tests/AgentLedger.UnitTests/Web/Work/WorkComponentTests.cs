using AgentLedger.UseCases.Work;
using AgentLedger.UseCases.Work.GetSession;
using AgentLedger.UseCases.Work.GetStep;
using AgentLedger.UseCases.Work.ListSessions;
using AgentLedger.UseCases.Work.ListSteps;
using AgentLedger.UseCases.Work.ListTurns;
using AgentLedger.Web.Components.Pages.Work;
using AgentLedger.Web.Components.Shared;
using Ardalis.Result;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLedger.UnitTests.Web.Work;

// Test data for the work page's components (ADR 0023).
internal static class WorkData
{
  public static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
  public static readonly Guid SessionId = Guid.Parse("11111111-0000-0000-0000-000000000000");
  public static readonly Guid TurnId = Guid.Parse("22222222-0000-0000-0000-000000000000");
  public static readonly Guid StepId = Guid.Parse("33333333-0000-0000-0000-000000000000");

  public static SessionListItem Session(string title = "Fix the cart") =>
    new(SessionId, "ClaudeCode", "native", title, "github.com/acme/shop", "main", 1, T0, T0.AddMinutes(5));

  public static SessionDetail Detail() => new(Session(), "devbox", "eric", new Dictionary<string, int> { ["Completed"] = 1 }, 1, 0, []);

  public static TurnListItem Turn(string source = "Typed") =>
    new(TurnId, 1, "Completed", source, T0, T0.AddSeconds(30), "Make the total include tax", 1, 0);

  public static StepListItem[] Steps() =>
  [
    new(Guid.NewGuid(), "Prompt", T0, null, "Make the total include tax", null, null),
    new(Guid.NewGuid(), "Thinking", T0.AddSeconds(1), null, null, null, null),
    new(StepId, "ToolCall", T0.AddSeconds(2), T0.AddSeconds(4), "dotnet test", "Bash", false),
  ];

  public static StepDetail Step(string kind = "ToolCall", string? text = null, string? output = "boom", bool? succeeded = false,
    string? input = """{"command":"dotnet test"}""", string turnStatus = "Completed") =>
    new(StepId, kind, T0.AddSeconds(2), T0.AddSeconds(4), text, null, kind == "ToolCall" ? "Bash" : null, "toolu_1", input, output,
      succeeded, new WorkLocation(SessionId, TurnId), 1, T0, turnStatus, "Typed", null, [], []);
}

public sealed class TurnListRender : BunitContext
{
  [Fact]
  public void LinksEachTurnAndCollapsesTheExpandedOne()
  {
    var list = Render<TurnList>(p => p
      .Add(c => c.SessionId, WorkData.SessionId)
      .Add(c => c.Turns, [WorkData.Turn()])
      .Add(c => c.ExpandedTurnId, WorkData.TurnId)
      .Add(c => c.Steps, WorkData.Steps()));

    // Clicking the expanded turn collapses it: its link leaves the turn out.
    list.Find("a.turn-row").GetAttribute("href").ShouldBe($"/work?unit={WorkData.SessionId}");
    list.Find("a.turn-row").GetAttribute("aria-expanded").ShouldBe("true");
  }

  [Fact]
  public void ShowsStepsOneLineEach()
  {
    var list = Render<TurnList>(p => p
      .Add(c => c.SessionId, WorkData.SessionId)
      .Add(c => c.Turns, [WorkData.Turn()])
      .Add(c => c.ExpandedTurnId, WorkData.TurnId)
      .Add(c => c.Steps, WorkData.Steps())
      .Add(c => c.SelectedStepId, WorkData.StepId));

    var rows = list.FindAll("a.step-row");
    rows.Count.ShouldBe(3);
    rows[1].TextContent.ShouldContain("summary not available");
    rows[1].ClassList.ShouldContain("dim");
    rows[2].TextContent.ShouldContain("Bash");
    rows[2].TextContent.ShouldContain("✗ failed");
    rows[2].TextContent.ShouldContain("2.0s");
    rows[2].GetAttribute("aria-current").ShouldBe("true");
    rows[2].GetAttribute("data-history").ShouldBe("replace");
  }

  [Fact]
  public void NamesItsColumns()
  {
    var list = Render<TurnList>(p => p.Add(c => c.SessionId, WorkData.SessionId).Add(c => c.Turns, [WorkData.Turn()]));

    list.Find(".turn-header").TextContent.ShouldContain("Time (UTC)");
  }

  [Fact]
  public void MarksEachNewDay()
  {
    // A session can span days: a divider at the top and wherever the day changes.
    var first = WorkData.Turn();
    var sameDay = first with { TurnId = Guid.NewGuid(), Number = 2, StartedAt = first.StartedAt.AddHours(3) };
    var nextDay = first with { TurnId = Guid.NewGuid(), Number = 3, StartedAt = first.StartedAt.AddDays(1) };

    var list = Render<TurnList>(p => p.Add(c => c.SessionId, WorkData.SessionId).Add(c => c.Turns, [first, sameDay, nextDay]));

    list.FindAll(".day").Select(d => d.TextContent.Trim()).ShouldBe(["Tue 27 Oct 2026", "Wed 28 Oct 2026"]);
  }

  [Fact]
  public void ShowsTheFullTimeOnHover()
  {
    var list = Render<TurnList>(p => p.Add(c => c.SessionId, WorkData.SessionId).Add(c => c.Turns, [WorkData.Turn()]));

    var time = list.Find("time.time");
    time.TextContent.ShouldBe("12:00");
    time.GetAttribute("title").ShouldBe("2026-10-27 12:00:00 UTC");
  }

  [Fact]
  public void LabelsPromptsNotTypedByAPerson()
  {
    var list = Render<TurnList>(p => p
      .Add(c => c.SessionId, WorkData.SessionId)
      .Add(c => c.Turns, [WorkData.Turn("Injected")]));

    list.Find(".source").TextContent.ShouldBe("Injected");
    list.Find("li[data-turn-row]").ClassList.ShouldContain("injected");
  }
}

public sealed class StepDetailsRender : BunitContext
{
  public StepDetailsRender() => Services.AddSingleton(Substitute.For<IMediator>());

  [Fact]
  public void ShowsAFailedToolCallsErrorAsAnAlert()
  {
    var details = Render<StepDetails>(p => p.Add(c => c.Step, WorkData.Step()).Add(c => c.CloseHref, "/work"));

    details.Find("#output-heading").TextContent.ShouldContain("Error");
    details.Find("pre[role=alert]").TextContent.ShouldBe("boom");
    details.Find("dl.fields dt").TextContent.ShouldBe("command");
  }

  [Theory]
  [InlineData("Completed", "No result (interrupted).")]
  [InlineData("InProgress", "Still running.")]
  public void SaysWhyThereIsNoResult(string turnStatus, string expected)
  {
    var details = Render<StepDetails>(p => p
      .Add(c => c.Step, WorkData.Step(output: null, succeeded: null, turnStatus: turnStatus)).Add(c => c.CloseHref, "/work"));

    details.Markup.ShouldContain(expected);
  }

  [Fact]
  public void ShowsLongOutputInPartUntilAsked()
  {
    var output = string.Join("\n", Enumerable.Range(1, 250).Select(i => $"line {i}"));
    var details = Render<StepDetails>(p => p.Add(c => c.Step, WorkData.Step(output: output, succeeded: true)).Add(c => c.CloseHref, "/work"));

    details.Find("pre:not([role])").TextContent.ShouldNotContain("line 201");
    details.Find("button:contains('Show all')").Click();

    details.FindAll("pre").Last().TextContent.ShouldContain("line 250");
  }

  [Fact]
  public void ExplainsMissingThinking()
  {
    var details = Render<StepDetails>(p => p.Add(c => c.Step, WorkData.Step("Thinking", text: null)).Add(c => c.CloseHref, "/work"));

    details.Markup.ShouldContain("showThinkingSummaries");
  }

  [Fact]
  public void ClosesWithALinkThatReplacesTheHistoryEntry()
  {
    var details = Render<StepDetails>(p => p.Add(c => c.Step, WorkData.Step()).Add(c => c.CloseHref, "/work?unit=x"));

    var close = details.Find("a[data-close]");
    close.GetAttribute("href").ShouldBe("/work?unit=x");
    close.GetAttribute("data-history").ShouldBe("replace");
  }
}

public sealed class MarkdownTextRender : BunitContext
{
  [Fact]
  public void SwitchesBetweenRenderedAndPlain()
  {
    var text = Render<MarkdownText>(p => p.Add(c => c.Text, "Use **tax**"));

    text.Find(".rendered").InnerHtml.ShouldContain("<strong>tax</strong>");
    text.Find("button:contains('Plain')").Click();

    text.Find("pre.plain").TextContent.ShouldBe("Use **tax**");
  }
}

public sealed class WorkPageRender : BunitContext
{
  private readonly IMediator _mediator = Substitute.For<IMediator>();

  public WorkPageRender()
  {
    Services.AddSingleton(_mediator);
    AddAuthorization().SetAuthorized("tester");
    JSInterop.Mode = JSRuntimeMode.Loose;
    _mediator.Send(Arg.Any<ListSessionsQuery>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success(new SessionPage([WorkData.Session()], null)));
    _mediator.Send(Arg.Any<GetSessionQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(WorkData.Detail()));
    _mediator.Send(Arg.Any<ListTurnsQuery>(), Arg.Any<CancellationToken>())
      .Returns(Result<IReadOnlyList<TurnListItem>>.Success([WorkData.Turn()]));
    _mediator.Send(Arg.Any<ListStepsQuery>(), Arg.Any<CancellationToken>())
      .Returns(Result<IReadOnlyList<StepListItem>>.Success(WorkData.Steps()));
    _mediator.Send(Arg.Any<GetStepQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(WorkData.Step()));
  }

  // Query-string parameters come from the (fake) browser's address, as on the real page.
  private IRenderedComponent<WorkPage> RenderAt(string url)
  {
    Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo(url);
    return Render<WorkPage>();
  }

  [Fact]
  public void ShowsTheSessionInTheUrl()
  {
    var page = RenderAt($"/work?unit={WorkData.SessionId}");

    page.Find(".summary h1").TextContent.ShouldContain("Fix the cart");
    page.FindAll("a.turn-row").Count.ShouldBe(1);
    page.FindAll("a.step-row").ShouldBeEmpty();
  }

  [Fact]
  public async Task ExpandsATurnWhenItsLinkIsFollowed()
  {
    var page = RenderAt($"/work?unit={WorkData.SessionId}");

    await page.InvokeAsync(() => page.Instance.GoAsync($"http://localhost/work?unit={WorkData.SessionId}&turn={WorkData.TurnId}"));

    page.FindAll("a.step-row").Count.ShouldBe(3);
    await _mediator.Received(1).Send(Arg.Any<GetSessionQuery>(), Arg.Any<CancellationToken>()); // the session isn't reloaded
  }

  [Fact]
  public async Task OpensAndClosesTheDetailsPane()
  {
    var page = RenderAt($"/work?unit={WorkData.SessionId}&turn={WorkData.TurnId}");

    await page.InvokeAsync(() => page.Instance.GoAsync(
      $"http://localhost/work?unit={WorkData.SessionId}&turn={WorkData.TurnId}&step={WorkData.StepId}"));
    page.FindAll(".pane.details").Count.ShouldBe(1);

    await page.InvokeAsync(() => page.Instance.GoAsync($"http://localhost/work?unit={WorkData.SessionId}&turn={WorkData.TurnId}"));
    page.FindAll(".pane.details").ShouldBeEmpty();
    page.Find(".work-layout").ClassList.ShouldContain("no-details");
  }

  [Fact]
  public void StartsItsScriptWithTheLayout()
  {
    RenderAt($"/work?unit={WorkData.SessionId}");

    JSInterop.VerifyInvoke("import").Arguments[0].ShouldBe("./Components/Pages/Work/WorkPage.razor.js");
  }
}
