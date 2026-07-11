using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.Sandbox.Components;

namespace CanDoItAll.FileTools.Sandbox.Demo;

public static class SandboxInteractionComposition
{
    public const string MermaidProfileId = "sandbox-mermaid";
    public const string AutoSaveProfileId = "sandbox-autosave";
    public const string BinaryProfileId = "sandbox-binary";

    public static FileInteractionComponentComposition Create()
        => new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .AddProfile(new FileInteractionProfileDescriptor(
                MermaidProfileId,
                FileInteractionCapabilities.View
                    | FileInteractionCapabilities.Edit
                    | FileInteractionCapabilities.Preview
                    | FileInteractionCapabilities.Save
                    | FileInteractionCapabilities.Undo
                    | FileInteractionCapabilities.Redo,
                extensions: [".mmd", ".mermaid"],
                mediaTypes: ["text/x-mermaid"],
                priority: 200,
                preview: new FilePreviewOptions(
                    enabled: true,
                    debounce: TimeSpan.FromMilliseconds(360),
                    splitByDefault: true,
                    placement: FilePreviewPlacement.Beside),
                history: new FileHistoryOptions(40, 512 * 1024)))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "sandbox-mermaid-view",
                MermaidProfileId,
                FileInteractionMode.View,
                typeof(SandboxMermaidView),
                FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "sandbox-mermaid-edit",
                MermaidProfileId,
                FileInteractionMode.Edit,
                typeof(TextFileEditor),
                FileInteractionContentKind.Text))
            .AddProfile(new FileInteractionProfileDescriptor(
                AutoSaveProfileId,
                FileInteractionCapabilities.View
                    | FileInteractionCapabilities.Edit
                    | FileInteractionCapabilities.Save
                    | FileInteractionCapabilities.Undo
                    | FileInteractionCapabilities.Redo,
                extensions: [".auto"],
                mediaTypes: ["text/x-sandbox-auto"],
                priority: 200,
                autoSave: new FileAutoSaveOptions(
                    FileAutoSaveTriggers.Idle | FileAutoSaveTriggers.TextUnitCount,
                    idleDelay: TimeSpan.FromMilliseconds(720),
                    textUnitCount: 8),
                history: new FileHistoryOptions(30, 256 * 1024)))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "sandbox-autosave-view",
                AutoSaveProfileId,
                FileInteractionMode.View,
                typeof(TextFileView),
                FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "sandbox-autosave-edit",
                AutoSaveProfileId,
                FileInteractionMode.Edit,
                typeof(TextFileEditor),
                FileInteractionContentKind.Text))
            .AddProfile(new FileInteractionProfileDescriptor(
                BinaryProfileId,
                FileInteractionCapabilities.View
                    | FileInteractionCapabilities.Edit
                    | FileInteractionCapabilities.Save
                    | FileInteractionCapabilities.Undo
                    | FileInteractionCapabilities.Redo,
                extensions: [".bin"],
                mediaTypes: ["application/octet-stream"],
                priority: 200,
                history: new FileHistoryOptions(20, 256 * 1024)))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "sandbox-binary-view",
                BinaryProfileId,
                FileInteractionMode.View,
                typeof(SandboxHexView),
                FileInteractionContentKind.Binary))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "sandbox-binary-edit",
                BinaryProfileId,
                FileInteractionMode.Edit,
                typeof(SandboxHexEditor),
                FileInteractionContentKind.Binary))
            .Build();
}
