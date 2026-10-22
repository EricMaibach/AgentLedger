using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

public sealed class JsonDisplayIndent
{
  [Fact]
  public void IndentsJson()
  {
    JsonDisplay.Indent("""{"a":1,"b":[true,null]}""").ShouldBe("""
      {
        "a": 1,
        "b": [
          true,
          null
        ]
      }
      """.ReplaceLineEndings("\n"));
  }

  [Fact]
  public void KeepsKeyOrderAndDuplicateKeys()
  {
    // The formatted view must not "correct" what the agent sent; that's what the exact view is for,
    // but formatting mustn't hide differences either (docs/ui.md, JsonViewer).
    JsonDisplay.Indent("""{"z":1,"a":2,"a":3}""").ShouldBe("{\n  \"z\": 1,\n  \"a\": 2,\n  \"a\": 3\n}");
  }

  [Fact]
  public void LeavesTextThatIsNotJsonUnchanged()
  {
    JsonDisplay.Indent("not json {").ShouldBe("not json {");
  }
}
