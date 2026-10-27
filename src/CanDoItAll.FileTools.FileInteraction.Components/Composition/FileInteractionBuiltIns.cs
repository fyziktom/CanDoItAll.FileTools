namespace CanDoItAll.FileTools.FileInteraction.Components;

public static class FileInteractionBuiltInProfileIds
{
    public const string Text = "base-text";
    public const string Image = "base-image";
    public const string Svg = "base-svg-inert";
    public const string Pdf = "base-pdf";
    public const string Audio = "base-audio";
    public const string Video = "base-video";
    public const string Object = "base-object";
}

public static class FileInteractionBuiltIns
{
    public static FileInteractionComponentBuilder AddBuiltIns(this FileInteractionComponentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .AddProfile(new FileInteractionProfileDescriptor(
                FileInteractionBuiltInProfileIds.Text,
                FileInteractionCapabilities.View
                    | FileInteractionCapabilities.Edit
                    | FileInteractionCapabilities.Preview
                    | FileInteractionCapabilities.Save
                    | FileInteractionCapabilities.Undo
                    | FileInteractionCapabilities.Redo,
                extensions:
                [
                    ".txt", ".log", ".md", ".mmd", ".json", ".xml", ".yaml", ".yml",
                    ".csv", ".cs", ".razor", ".html", ".htm", ".css", ".js", ".ts"
                ],
                mediaTypes: ["text/*", "application/json", "application/xml"],
                preview: new FilePreviewOptions(enabled: true, debounce: TimeSpan.FromMilliseconds(300)),
                history: new FileHistoryOptions(maxEntries: 50, maxBytes: 2 * 1024 * 1024)))
            .AddProfile(new FileInteractionProfileDescriptor(
                FileInteractionBuiltInProfileIds.Image,
                FileInteractionCapabilities.View,
                extensions: [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"],
                mediaTypes: ["image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp"]))
            .AddProfile(new FileInteractionProfileDescriptor(
                FileInteractionBuiltInProfileIds.Svg,
                FileInteractionCapabilities.View,
                extensions: [".svg"],
                mediaTypes: ["image/svg+xml"],
                priority: 100))
            .AddProfile(new FileInteractionProfileDescriptor(
                FileInteractionBuiltInProfileIds.Pdf,
                FileInteractionCapabilities.View,
                extensions: [".pdf"],
                mediaTypes: ["application/pdf"],
                priority: 10))
            .AddProfile(new FileInteractionProfileDescriptor(
                FileInteractionBuiltInProfileIds.Audio,
                FileInteractionCapabilities.View,
                extensions: [".wav", ".mp3", ".m4a", ".aac", ".ogg", ".flac"],
                mediaTypes: ["audio/*"]))
            .AddProfile(new FileInteractionProfileDescriptor(
                FileInteractionBuiltInProfileIds.Video,
                FileInteractionCapabilities.View,
                extensions: [".mp4", ".webm", ".avi", ".mov", ".mkv"],
                mediaTypes: ["video/*"]))
            .AddProfile(new FileInteractionProfileDescriptor(
                FileInteractionBuiltInProfileIds.Object,
                FileInteractionCapabilities.View,
                extensions: ["*"],
                mediaTypes: ["*/*"],
                priority: -100))
            .AddHistoryFactory(new BoundedTextHistoryProviderFactory())
            .AddRenderer(new FileInteractionRendererDescriptor(
                "base-text-view", FileInteractionBuiltInProfileIds.Text, FileInteractionMode.View,
                typeof(TextFileView), FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "base-text-edit", FileInteractionBuiltInProfileIds.Text, FileInteractionMode.Edit,
                typeof(TextFileEditor), FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "base-image-view", FileInteractionBuiltInProfileIds.Image, FileInteractionMode.View,
                typeof(ImageFileView), FileInteractionContentKind.Binary))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "base-svg-inert-view", FileInteractionBuiltInProfileIds.Svg, FileInteractionMode.View,
                typeof(SvgFileView), FileInteractionContentKind.Binary))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "base-pdf-view", FileInteractionBuiltInProfileIds.Pdf, FileInteractionMode.View,
                typeof(PdfFileView), FileInteractionContentKind.Binary))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "base-audio-view", FileInteractionBuiltInProfileIds.Audio, FileInteractionMode.View,
                typeof(AudioFileView), FileInteractionContentKind.Binary))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "base-video-view", FileInteractionBuiltInProfileIds.Video, FileInteractionMode.View,
                typeof(VideoFileView), FileInteractionContentKind.Binary))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "base-object-view", FileInteractionBuiltInProfileIds.Object, FileInteractionMode.View,
                typeof(ObjectFileView), FileInteractionContentKind.Binary));

        return builder;
    }

    public static FileInteractionComponentBuilder AddMetadataFallback(this FileInteractionComponentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddRenderer(new FileInteractionRendererDescriptor(
            "base-object-metadata-view", FileInteractionBuiltInProfileIds.Object, FileInteractionMode.View,
            typeof(FileMetadataView), FileInteractionContentKind.Binary, priority: 100,
            contentRequirement: FileInteractionContentRequirement.MetadataOnly));
    }
}
