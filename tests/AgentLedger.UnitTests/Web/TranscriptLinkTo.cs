using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

// The transcript page's URL carries the file, view and cursor, leaving defaults out (docs/ui.md).
public sealed class TranscriptLinkTo
{
  [Fact]
  public void IsTheSessionsMainTranscriptByDefault()
  {
    TranscriptLink.To("3829bce8-470e").ShouldBe("/raw/sessions/3829bce8-470e/transcript");
  }

  [Fact]
  public void SelectsAFileAViewAndAPage()
  {
    TranscriptLink.To("s1", file: "subagents/agent-a1", exact: true, after: "abc")
      .ShouldBe("/raw/sessions/s1/transcript?file=subagents%2Fagent-a1&view=exact&after=abc");
  }

  [Fact]
  public void LeavesOutTheMainFile()
  {
    TranscriptLink.To("s1", file: "main").ShouldBe("/raw/sessions/s1/transcript");
  }

  [Fact]
  public void EscapesTheSession()
  {
    TranscriptLink.To("a b/c").ShouldBe("/raw/sessions/a%20b%2Fc/transcript");
  }
}
