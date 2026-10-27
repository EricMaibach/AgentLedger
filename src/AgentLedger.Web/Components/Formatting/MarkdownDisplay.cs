using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace AgentLedger.Web.Components.Formatting;

/// <summary>
/// Prompts, responses and thinking as HTML (ADR 0023). The text is untrusted (anything an agent read or was told), so:
/// raw HTML is shown as text; only http, https and mailto links become links, opening in a new tab without referrer or
/// opener; images are never loaded, but shown as links labelled with their alt text.
/// </summary>
public static class MarkdownDisplay
{
  private static readonly string[] SafeSchemes = ["http", "https", "mailto"];

  // Only the extensions listed: some others (generic attributes) would let text add attributes to elements.
  private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
    .DisableHtml()
    .UsePipeTables()
    .UseTaskLists()
    .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
    .UseAutoLinks()
    .Build();

  public static MarkupString Render(string markdown)
  {
    var document = Markdown.Parse(markdown, Pipeline);
    foreach (var link in document.Descendants<LinkInline>().ToList())
      MakeSafe(link);
    foreach (var autolink in document.Descendants<AutolinkInline>().ToList())
      MakeSafe(autolink);
    return new MarkupString(document.ToHtml(Pipeline));
  }

  private static void MakeSafe(LinkInline link)
  {
    // An image becomes a link to it, labelled with its alt text (its children).
    link.IsImage = false;
    if (IsSafe(link.Url))
    {
      OpenSeparately(link);
      return;
    }

    // Unsafe or relative: keep the label as text.
    var child = link.FirstChild;
    var after = (Inline)link;
    while (child is not null)
    {
      var next = child.NextSibling;
      child.Remove();
      after.InsertAfter(child);
      after = child;
      child = next;
    }
    link.Remove();
  }

  private static void MakeSafe(AutolinkInline link)
  {
    if (IsSafe(link.Url))
      OpenSeparately(link);
    else
      link.ReplaceBy(new LiteralInline(link.Url));
  }

  private static void OpenSeparately(Inline link)
  {
    var attributes = link.GetAttributes();
    attributes.AddPropertyIfNotExist("target", "_blank");
    attributes.AddPropertyIfNotExist("rel", "noopener noreferrer nofollow");
  }

  private static bool IsSafe(string? url) =>
    Uri.TryCreate(url, UriKind.Absolute, out var uri) && SafeSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
}
