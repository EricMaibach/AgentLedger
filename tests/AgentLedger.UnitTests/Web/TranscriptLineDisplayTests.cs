using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

// How a transcript line is summarized in the list (docs/ui.md, TranscriptLines). Raw: the line's own field, as written.
public sealed class TranscriptLineDisplayTypeOf
{
  [Theory]
  [InlineData("""{"type":"assistant","message":{}}""", "assistant")]
  [InlineData("""{"parentUuid":null,"type":"user"}""", "user")]
  public void IsTheLinesOwnTypeField(string line, string expected)
  {
    TranscriptLineDisplay.TypeOf(line).ShouldBe(expected);
  }

  [Theory]
  [InlineData("""{"message":{}}""")]       // no type field
  [InlineData("""{"type":42}""")]          // not a string
  [InlineData("""["type","user"]""")]      // not an object
  public void IsNothingWithoutAStringTypeField(string line)
  {
    TranscriptLineDisplay.TypeOf(line).ShouldBeNull();
  }

  [Theory]
  [InlineData("{\"type\":\"user\", not json")]
  [InlineData("")]
  public void SaysWhenTheLineIsNotJson(string line)
  {
    TranscriptLineDisplay.TypeOf(line).ShouldBe(TranscriptLineDisplay.NotJson);
  }
}

public sealed class TranscriptLineDisplayPreview
{
  [Fact]
  public void IsTheWholeLineWhenShort()
  {
    TranscriptLineDisplay.Preview("""{"type":"user"}""").ShouldBe("""{"type":"user"}""");
  }

  [Fact]
  public void CutsALongLineWithAnEllipsis()
  {
    var preview = TranscriptLineDisplay.Preview(new string('x', 500));

    preview.Length.ShouldBe(TranscriptLineDisplay.PreviewLength + 1);
    preview.ShouldEndWith("…");
  }

  [Fact]
  public void KeepsItOnOneLine()
  {
    TranscriptLineDisplay.Preview("a\r\nb\tc").ShouldBe("a b c");
  }
}
