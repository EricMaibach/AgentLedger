using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

// How the work pages show times, sizes and names (ADR 0023).
public sealed class WorkFormatTests
{
  private static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

  [Theory]
  [InlineData(0, "+0:00")]
  [InlineData(5.7, "+0:05")]
  [InlineData(723, "+12:03")]
  [InlineData(3723, "+1:02:03")]
  public void OffsetsAreTimeSinceTheTurnStarted(double seconds, string expected)
  {
    WorkFormat.Offset(T0, T0.AddSeconds(seconds)).ShouldBe(expected);
  }

  [Theory]
  [InlineData(0.4, "0.4s")]
  [InlineData(9.95, "9.9s")]
  [InlineData(48, "48s")]
  [InlineData(130, "2m 10s")]
  [InlineData(3780, "1h 03m")]
  public void DurationsAreShort(double seconds, string expected)
  {
    WorkFormat.Duration(T0, T0.AddSeconds(seconds)).ShouldBe(expected);
  }

  [Fact]
  public void NoEndMeansNoDuration()
  {
    WorkFormat.Duration(T0, null).ShouldBeNull();
  }

  [Theory]
  [InlineData(3, "3")]
  [InlineData(999, "999")]
  [InlineData(2_340, "2.3k")]
  [InlineData(551_000, "551k")]
  [InlineData(1_544_754, "1.5M")]
  [InlineData(537_309_258, "537M")]
  public void TokenCountsAreRounded(long tokens, string expected)
  {
    WorkFormat.Tokens(tokens).ShouldBe(expected);
  }

  [Theory]
  [InlineData("claude-opus-5-5", "Opus 5.5")]
  [InlineData("claude-sonnet-5", "Sonnet 5")]
  [InlineData("claude-haiku-4-5-20251001", "Haiku 4.5")]
  [InlineData("claude-fable-5-1", "Fable 5.1")]
  [InlineData("gpt-5-codex", "gpt-5-codex")]
  public void ModelNamesAreShort(string model, string expected)
  {
    WorkFormat.Model(model).ShouldBe(expected);
  }

  [Theory]
  [InlineData("github.com/EricMaibach/AgentLedger", "AgentLedger")]
  [InlineData("gitlab.example.com/team/shop.git", "shop")]
  [InlineData("devbox:/home/eric/scratch", "scratch")]
  [InlineData(null, null)]
  public void ProjectsAreNamedByTheirLastPart(string? repository, string? expected)
  {
    WorkFormat.Project(repository).ShouldBe(expected);
  }

  [Fact]
  public void SimpleToolInputBecomesNamesAndValues()
  {
    WorkFormat.InputPairs("""{"command":"ls -la","timeout":5000,"run_in_background":false}""")
      .ShouldBe([("command", "ls -la"), ("timeout", "5000"), ("run_in_background", "false")]);
  }

  [Theory]
  [InlineData("""{"edits":[{"a":1}]}""")]
  [InlineData("""{"options":{"x":1}}""")]
  [InlineData("[1,2]")]
  [InlineData("not json")]
  [InlineData(null)]
  public void NestedOrOddInputIsShownAsJson(string? input)
  {
    WorkFormat.InputPairs(input).ShouldBeNull();
  }

  [Fact]
  public void LongOutputIsCutAtALineLimit()
  {
    var text = string.Join("\n", Enumerable.Range(1, 250).Select(i => $"line {i}"));

    var (shown, hidden) = WorkFormat.Preview(text, 200);

    shown.Split('\n').Length.ShouldBe(200);
    shown.ShouldEndWith("line 200");
    hidden.ShouldBe(50);
    WorkFormat.Preview("a\nb", 200).ShouldBe(("a\nb", 0));
  }
}

// Links within the work section (ADR 0023): the selection lives in the query string.
public sealed class WorkLinkTo
{
  private static readonly Guid Unit = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid Turn = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid Step = Guid.Parse("33333333-3333-3333-3333-333333333333");

  [Fact]
  public void BuildsTheSelectionIntoTheQueryString()
  {
    WorkLink.To(Unit).ShouldBe($"/work?unit={Unit}");
    WorkLink.To(Unit, Turn).ShouldBe($"/work?unit={Unit}&turn={Turn}");
    WorkLink.To(Unit, Turn, Step).ShouldBe($"/work?unit={Unit}&turn={Turn}&step={Step}");
  }

  [Fact]
  public void LinksToRawEvidence()
  {
    WorkLink.RawEvent(Step).ShouldBe($"/raw/events/{Step}");
    WorkLink.RawLine(Step).ShouldBe($"/raw/transcripts/{Step}");
  }
}
