using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown.Components;

namespace CanDoItAll.FileTools.FileInteraction.Markdown;

public static class FileInteractionMarkdownProfileIds
{
    public const string Markdown = "markdown";
}

/// <summary>Adds the optional Markdown profile and renderer to an explicit component composition.</summary>
public static class FileInteractionMarkdownExtensions
{
    public static FileInteractionComponentBuilder AddMarkdown(
        this FileInteractionComponentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddProfile(new FileInteractionProfileDescriptor(
                FileInteractionMarkdownProfileIds.Markdown,
                FileInteractionCapabilities.View
                    | FileInteractionCapabilities.Edit
                    | FileInteractionCapabilities.Preview
                    | FileInteractionCapabilities.Save
                    | FileInteractionCapabilities.Undo
                    | FileInteractionCapabilities.Redo,
                extensions: [".md", ".markdown"],
                mediaTypes: ["text/markdown"],
                priority: 100,
                preview: new FilePreviewOptions(
                    enabled: true,
                    debounce: TimeSpan.FromMilliseconds(300),
                    splitByDefault: true,
                    placement: FilePreviewPlacement.Beside),
                history: new FileHistoryOptions(
                    maxEntries: 50,
                    maxBytes: 2 * 1024 * 1024)))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "markdown-view",
                FileInteractionMarkdownProfileIds.Markdown,
                FileInteractionMode.View,
                typeof(MarkdownFileView),
                FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "markdown-edit",
                FileInteractionMarkdownProfileIds.Markdown,
                FileInteractionMode.Edit,
                typeof(TextFileEditor),
                FileInteractionContentKind.Text));
    }
}
