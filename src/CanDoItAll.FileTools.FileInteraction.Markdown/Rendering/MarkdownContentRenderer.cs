using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Markdown;

/// <summary>
/// Converts Markdown to a deliberately inert HTML subset. Raw HTML is disabled and link/image
/// destinations are never emitted, so the host remains the sole navigation and fetch authority.
/// </summary>
internal static class MarkdownContentRenderer
{
    private static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .Use(new InertLinkPipelineExtension())
        .Build();

    public static MarkupString ToMarkup(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return new MarkupString(global::Markdig.Markdown.ToHtml(markdown, pipeline));
    }

    private sealed class InertLinkPipelineExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipelineBuilder)
        {
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is not HtmlRenderer htmlRenderer)
            {
                return;
            }

            htmlRenderer.ObjectRenderers.Replace<LinkInlineRenderer>(new InertLinkInlineRenderer());
            htmlRenderer.ObjectRenderers.Replace<AutolinkInlineRenderer>(new InertAutolinkInlineRenderer());
        }
    }

    private sealed class InertLinkInlineRenderer : HtmlObjectRenderer<LinkInline>
    {
        protected override void Write(HtmlRenderer renderer, LinkInline link)
        {
            var cssClass = link.IsImage
                ? "cdi-ft-markdown__image-label"
                : "cdi-ft-markdown__link-label";

            renderer.Write("<span class=\"");
            renderer.Write(cssClass);
            renderer.Write("\">");
            if (link.IsImage)
            {
                renderer.Write("Image: ");
            }

            renderer.WriteChildren(link);
            renderer.Write("</span>");
        }
    }

    private sealed class InertAutolinkInlineRenderer : HtmlObjectRenderer<AutolinkInline>
    {
        protected override void Write(HtmlRenderer renderer, AutolinkInline autolink)
        {
            renderer.Write("<span class=\"cdi-ft-markdown__link-label\">");
            renderer.WriteEscape(autolink.Url);
            renderer.Write("</span>");
        }
    }
}
