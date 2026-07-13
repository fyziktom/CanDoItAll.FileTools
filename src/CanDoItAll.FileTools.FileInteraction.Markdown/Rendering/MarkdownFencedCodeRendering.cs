using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Markdown;

public sealed record MarkdownFencedCodeRenderContext(
    FileInteractionRenderContext File,
    string Language,
    string Source);

public interface IMarkdownFencedCodeComponent : IComponent
{
    MarkdownFencedCodeRenderContext Context { get; set; }
}

public interface IMarkdownFencedCodeComponentRegistration
{
    string Language { get; }

    Type ComponentType { get; }
}

internal sealed record MarkdownFencedCodeBlock(string Language, string Source);

internal sealed record MarkdownRenderedSegment(
    string? HtmlContent,
    MarkdownFencedCodeBlock? FencedCodeBlock)
{
    public static MarkdownRenderedSegment Html(string content) => new(content, null);

    public static MarkdownRenderedSegment FencedCode(MarkdownFencedCodeBlock block) => new(null, block);
}

internal static class MarkdownFencedCodeLanguage
{
    public static bool TryNormalize(string? info, out string language)
    {
        language = string.Empty;
        if (string.IsNullOrWhiteSpace(info))
        {
            return false;
        }

        var value = info.Trim();
        var separatorIndex = value.IndexOfAny([' ', '\t']);
        language = (separatorIndex < 0 ? value : value[..separatorIndex]).Trim().ToLowerInvariant();
        return language.Length > 0;
    }
}
