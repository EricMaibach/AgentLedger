using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

// Prompts, responses and thinking rendered as Markdown (ADR 0023). The text is untrusted: it can hold anything an
// agent read or was told, so the renderer must never let it run script, load anything, or navigate anywhere unexpected.
public sealed class MarkdownDisplayRender
{
  private static string Render(string markdown) => MarkdownDisplay.Render(markdown).Value;

  [Fact]
  public void RendersCommonMarkdown()
  {
    var html = Render("# Plan\n\n- one\n- **two**\n\n```cs\nvar x = 1;\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |");

    html.ShouldContain("<h1");
    html.ShouldContain("<li><strong>two</strong></li>");
    html.ShouldContain("<code class=\"language-cs\">var x = 1;");
    html.ShouldContain("<table>");
  }

  [Theory]
  [InlineData("<script>alert(1)</script>")]
  [InlineData("Hello <img src=x onerror=alert(1)> there")]
  [InlineData("<div onclick=\"alert(1)\">click</div>")]
  public void ShowsRawHtmlAsText(string markdown)
  {
    var html = Render(markdown);

    html.ShouldNotContain("<script");
    html.ShouldNotContain("<img");
    html.ShouldNotContain("<div");
    html.ShouldContain("&lt;");
  }

  [Fact]
  public void OpensWebLinksInANewTabWithoutReferrerOrOpener()
  {
    var html = Render("[docs](https://example.com/a)");

    html.ShouldContain("href=\"https://example.com/a\"");
    html.ShouldContain("target=\"_blank\"");
    html.ShouldContain("rel=\"noopener noreferrer nofollow\"");
  }

  [Theory]
  [InlineData("[x](javascript:alert(1))")]
  [InlineData("[x](JavaScript:alert(1))")]
  [InlineData("[x](data:text/html;base64,PHNjcmlwdD4=)")]
  [InlineData("[x](vbscript:msgbox)")]
  [InlineData("[x](/settings/api-keys)")]
  [InlineData("[x][r]\n\n[r]: javascript:alert(1)")]
  public void KeepsOtherLinksAsText(string markdown)
  {
    var html = Render(markdown);

    html.ShouldNotContain("href");
    html.ShouldContain("x");
  }

  [Fact]
  public void AllowsMailLinks()
  {
    Render("[mail](mailto:a@example.com)").ShouldContain("href=\"mailto:a@example.com\"");
  }

  [Theory]
  [InlineData("![tracking pixel](https://example.com/p.png)", "https://example.com/p.png")]
  [InlineData("![chart](data:image/png;base64,iVBOR)", null)]
  public void NeverLoadsImages(string markdown, string? link)
  {
    // Loading an image would tell whoever planted it in a transcript that someone is reading it.
    var html = Render(markdown);

    html.ShouldNotContain("<img");
    html.ShouldContain(markdown.Contains("tracking") ? "tracking pixel" : "chart");
    if (link is null)
      html.ShouldNotContain("href");
    else
      html.ShouldContain($"href=\"{link}\"");
  }

  [Fact]
  public void AutolinksWebAddresses()
  {
    Render("See https://example.com/x for more").ShouldContain("href=\"https://example.com/x\"");
  }

  [Fact]
  public void KeepsTheTextOfAnUnsafeAutolink()
  {
    var html = Render("<javascript:alert(1)>");

    html.ShouldNotContain("href");
  }
}
