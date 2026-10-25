using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

// Links between raw transcript pages carry the filters in the query string, leaving defaults out (docs/ui.md).
public sealed class RawTranscriptsLinkTo
{
  [Fact]
  public void IsTheBareListWithoutFilters()
  {
    RawTranscriptsLink.To(new RawTranscriptsQueryString()).ShouldBe("/raw/transcripts");
  }

  [Fact]
  public void IncludesOnlyTheParametersThatAreSet()
  {
    RawTranscriptsLink.To(new RawTranscriptsQueryString(Session: "s1", File: "subagents/agent-a1", LineType: "assistant"))
      .ShouldBe("/raw/transcripts?lineType=assistant&session=s1&file=subagents%2Fagent-a1");
  }

  [Fact]
  public void LeavesOutTheDefaultPageSize()
  {
    RawTranscriptsLink.To(new RawTranscriptsQueryString(Limit: 10, After: "abc")).ShouldBe("/raw/transcripts?limit=10&after=abc");
  }

  [Fact]
  public void LinksToALine()
  {
    var lineId = Guid.Parse("15803c1c-b396-8d0e-864f-444c9cdc2157");

    RawTranscriptsLink.ToLine(lineId).ShouldBe("/raw/transcripts/15803c1c-b396-8d0e-864f-444c9cdc2157");
  }
}
