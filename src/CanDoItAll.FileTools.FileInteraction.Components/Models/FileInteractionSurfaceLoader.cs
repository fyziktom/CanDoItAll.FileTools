namespace CanDoItAll.FileTools.FileInteraction.Components;

internal sealed record FileInteractionSurfaceLoadResult(
    FileInteractionRequest Request,
    FileInteractionResolvedSurface Surface,
    ReadOnlyMemory<byte> Content)
{
    public bool IsResolved => Surface.IsResolved;
}

/// <summary>Loads bounded content and re-resolves against authoritative source metadata.</summary>
internal sealed class FileInteractionSurfaceLoader
{
    private readonly FileInteractionContentLoader contentLoader = new();

    public async ValueTask<FileInteractionSurfaceLoadResult> LoadAsync(
        FileInteractionRequest request,
        IFileContentSource source,
        FileInteractionComponentComposition composition,
        FileInteractionMode mode,
        int maximumContentBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(composition);
        var initialSurface = FileInteractionSurfaceResolver.Resolve(composition, request, mode);
        if (mode == FileInteractionMode.Diff && !initialSurface.IsResolved)
        {
            return new FileInteractionSurfaceLoadResult(request, initialSurface, ReadOnlyMemory<byte>.Empty);
        }

        FileInteractionRequest? effectiveRequest = null;
        FileInteractionResolvedSurface? finalSurface = null;
        var loaded = await contentLoader.LoadAsync(
            source,
            request,
            maximumContentBytes,
            (mediaType, revision) =>
            {
                effectiveRequest = FileInteractionRequestFactory.WithSourceMetadata(
                    request, mode, mediaType, revision);
                finalSurface = FileInteractionSurfaceResolver.Resolve(
                    composition, effectiveRequest, mode);
                return finalSurface.RendererResolution?.Renderer?.ContentRequirement
                    == FileInteractionContentRequirement.FullContent;
            },
            cancellationToken).ConfigureAwait(false);
        return new FileInteractionSurfaceLoadResult(
            effectiveRequest!,
            finalSurface!,
            loaded.Content);
    }
}
