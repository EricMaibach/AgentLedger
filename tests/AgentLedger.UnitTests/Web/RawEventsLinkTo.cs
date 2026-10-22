using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

// Links between raw event pages carry the filters in the query string (docs/ui.md, URLs).
public sealed class RawEventsLinkTo
{
  [Fact]
  public void IsTheBareListWithoutFilters()
  {
    RawEventsLink.To(new RawEventsQueryString()).ShouldBe("/raw/events");
  }

  [Fact]
  public void IncludesOnlyTheParametersThatAreSet()
  {
    var link = RawEventsLink.To(new RawEventsQueryString(Agent: "claude-code", Session: "abc"));

    link.ShouldBe("/raw/events?agent=claude-code&session=abc");
  }

  [Fact]
  public void LeavesOutDefaults()
  {
    // distinct=true and the default page size are the defaults, so they don't clutter shareable URLs.
    RawEventsLink.To(new RawEventsQueryString(Distinct: true)).ShouldBe("/raw/events");
    RawEventsLink.To(new RawEventsQueryString(Distinct: false)).ShouldBe("/raw/events?distinct=false");
  }

  [Fact]
  public void EscapesValues()
  {
    RawEventsLink.To(new RawEventsQueryString(Repo: "github.com/owner/repo", After: "a+b=")).ShouldBe(
      "/raw/events?repo=github.com%2Fowner%2Frepo&after=a%2Bb%3D");
  }
}
