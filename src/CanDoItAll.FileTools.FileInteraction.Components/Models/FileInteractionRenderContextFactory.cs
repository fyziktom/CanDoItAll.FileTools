using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Builds renderer contexts from one atomic edit or preview state.</summary>
internal static class FileInteractionRenderContextFactory
{
    public static FileInteractionRenderContext CreateMain(
        FileInteractionSurfaceBinding surface,
        FileInteractionEditingRuntime? editing,
        FileInteractionMode mode,
        ReadOnlyMemory<byte> content,
        string? text,
        int maximumContentBytes,
        EventCallback<string> textChanged,
        EventCallback<FileInteractionContentChange> contentChanged)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var sourceRequest = surface.Request
            ?? throw new InvalidOperationException("No file interaction request is active.");
        var currentSnapshot = editing?.State.Current;
        var mediaType = currentSnapshot?.MediaType ?? sourceRequest.MediaType;
        var encodingName = currentSnapshot?.EncodingName ?? surface.EncodingName;
        var request = FileInteractionRequestFactory.WithSourceMetadata(
            sourceRequest,
            mode,
            mediaType,
            editing is null ? sourceRequest.ContentRevision : editing.State.BaseRevision);
        return new FileInteractionRenderContext(
            request,
            mode,
            content,
            currentSnapshot?.EditRevision ?? 0,
            mediaType,
            text,
            encodingName,
            textChanged,
            contentChanged,
            maximumContentBytes);
    }

    public static FileInteractionRenderContext CreatePreview(
        FileInteractionSurfaceBinding surface,
        FileInteractionEditingRuntime editing,
        int maximumContentBytes)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(editing);
        var sourceRequest = surface.Request
            ?? throw new InvalidOperationException("No file interaction request is active.");
        var preview = editing.Preview?.Snapshot
            ?? throw new InvalidOperationException("No file preview is active.");
        var request = FileInteractionRequestFactory.WithSourceMetadata(
            sourceRequest,
            FileInteractionMode.View,
            preview.MediaType,
            editing.State.BaseRevision);
        return new FileInteractionRenderContext(
            request,
            FileInteractionMode.View,
            preview.Content,
            preview.EditRevision,
            preview.MediaType,
            preview.Text,
            preview.EncodingName,
            maximumContentBytes: maximumContentBytes);
    }
}
