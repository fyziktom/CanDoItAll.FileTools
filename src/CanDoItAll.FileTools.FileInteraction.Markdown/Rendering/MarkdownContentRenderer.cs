using System.Globalization;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CanDoItAll.FileTools.FileInteraction.Markdown;

/// <summary>
/// Converts Markdown to a deliberately inert HTML subset. Raw HTML is disabled and link/image
/// destinations are never emitted, so the host remains the sole navigation and fetch authority.
/// </summary>
internal static class MarkdownContentRenderer
{
    private static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Use(new InertLinkPipelineExtension())
        .Build();

    public static IReadOnlyList<MarkdownRenderedSegment> ToSegments(
        string markdown,
        IReadOnlySet<string> componentLanguages)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(componentLanguages);
        if (componentLanguages.Count == 0)
        {
            return [MarkdownRenderedSegment.Html(global::Markdig.Markdown.ToHtml(markdown, pipeline))];
        }

        var fencedCodeBlocks = new List<MarkdownFencedCodeBlock>();
        var markerPrefix = $"<!--cdi-ft-markdown-{Guid.NewGuid():N}-";
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);
        renderer.ObjectRenderers.Replace<CodeBlockRenderer>(new ExtractingCodeBlockRenderer(
            componentLanguages,
            fencedCodeBlocks,
            markerPrefix));
        renderer.Render(global::Markdig.Markdown.Parse(markdown, pipeline));
        writer.Flush();

        return SplitRenderedHtml(writer.ToString(), fencedCodeBlocks, markerPrefix);
    }

    private static IReadOnlyList<MarkdownRenderedSegment> SplitRenderedHtml(
        string html,
        IReadOnlyList<MarkdownFencedCodeBlock> fencedCodeBlocks,
        string markerPrefix)
    {
        if (fencedCodeBlocks.Count == 0)
        {
            return [MarkdownRenderedSegment.Html(html)];
        }

        var segments = new List<MarkdownRenderedSegment>((fencedCodeBlocks.Count * 2) + 1);
        var cursor = 0;
        for (var index = 0; index < fencedCodeBlocks.Count; index++)
        {
            var marker = CreateMarker(markerPrefix, index);
            var markerIndex = html.IndexOf(marker, cursor, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                throw new InvalidOperationException("Markdown fenced-code render marker was not preserved by Markdig.");
            }

            if (markerIndex > cursor)
            {
                segments.Add(MarkdownRenderedSegment.Html(html[cursor..markerIndex]));
            }

            segments.Add(MarkdownRenderedSegment.FencedCode(fencedCodeBlocks[index]));
            cursor = markerIndex + marker.Length;
        }

        if (cursor < html.Length)
        {
            segments.Add(MarkdownRenderedSegment.Html(html[cursor..]));
        }

        return segments;
    }

    private static string CreateMarker(string markerPrefix, int index) => $"{markerPrefix}{index}-->";

    private sealed class ExtractingCodeBlockRenderer(
        IReadOnlySet<string> componentLanguages,
        List<MarkdownFencedCodeBlock> fencedCodeBlocks,
        string markerPrefix) : CodeBlockRenderer
    {
        protected override void Write(HtmlRenderer renderer, CodeBlock codeBlock)
        {
            if (codeBlock is not FencedCodeBlock fencedCodeBlock ||
                !MarkdownFencedCodeLanguage.TryNormalize(fencedCodeBlock.Info?.ToString(), out var language) ||
                !componentLanguages.Contains(language))
            {
                base.Write(renderer, codeBlock);
                return;
            }

            var index = fencedCodeBlocks.Count;
            fencedCodeBlocks.Add(new MarkdownFencedCodeBlock(language, fencedCodeBlock.Lines.ToString()));
            renderer.Write(CreateMarker(markerPrefix, index));
        }
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
