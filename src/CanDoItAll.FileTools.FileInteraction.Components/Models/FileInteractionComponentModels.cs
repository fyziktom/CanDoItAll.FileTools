namespace CanDoItAll.FileTools.FileInteraction.Components;

internal enum FileInteractionLoadState
{
    Loading,
    Loaded,
    Unsupported,
    Error
}

internal enum FileInteractionSaveActivity
{
    Saved,
    Dirty,
    Saving,
    Failed,
    Conflict
}

internal sealed record FileInteractionInputStamp(
    FileInteractionRequest Request,
    IFileContentSource Source,
    FileInteractionComponentComposition Composition,
    int MaximumBytes)
{
    public bool Matches(FileInteractionInputStamp other)
        => Request.File == other.Request.File
            && string.Equals(Request.FileName, other.Request.FileName, StringComparison.Ordinal)
            && string.Equals(Request.MediaType, other.Request.MediaType, StringComparison.Ordinal)
            && Request.Size == other.Request.Size
            && Request.ContentRevision == other.Request.ContentRevision
            && ReferenceEquals(Source, other.Source)
            && ReferenceEquals(Composition, other.Composition)
            && MaximumBytes == other.MaximumBytes;
}

internal static class FileInteractionRequestFactory
{
    public static FileInteractionRequest WithMode(
        FileInteractionRequest request,
        FileInteractionMode mode)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new FileInteractionRequest(
            request.File,
            request.FileName,
            mode,
            request.MediaType,
            request.Size,
            request.ContentRevision);
    }

    /// <summary>Copies authoritative metadata already selected by the bounded content loader.</summary>
    public static FileInteractionRequest WithSourceMetadata(
        FileInteractionRequest request,
        FileInteractionMode mode,
        string? mediaType,
        FileContentRevision? contentRevision)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new FileInteractionRequest(
            request.File,
            request.FileName,
            mode,
            mediaType,
            request.Size,
            contentRevision);
    }
}

internal sealed record FileInteractionResolvedSurface(
    FileInteractionResolution ProfileResolution,
    FileInteractionRendererResolution? RendererResolution)
{
    public bool IsResolved => ProfileResolution.IsResolved && RendererResolution?.IsResolved == true;

    public string Message => ProfileResolution.Status switch
    {
        FileInteractionResolutionStatus.Ambiguous => "More than one file profile matched with equal priority.",
        FileInteractionResolutionStatus.Unsupported => "No profile supports this file and mode.",
        _ when RendererResolution?.Status == FileInteractionRendererResolutionStatus.Ambiguous
            => "More than one renderer matched with equal priority.",
        _ => "No renderer supports the selected profile and mode."
    };
}

/// <summary>Owns profile/renderer pairing so the Razor component only applies one resolved surface.</summary>
internal static class FileInteractionSurfaceResolver
{
    public static FileInteractionResolvedSurface Resolve(
        FileInteractionComponentComposition composition,
        FileInteractionRequest request,
        FileInteractionMode mode)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(request);
        var modeRequest = FileInteractionRequestFactory.WithMode(request, mode);
        var profileResolution = composition.Core.Profiles.Resolve(modeRequest);
        if (!profileResolution.IsResolved)
        {
            return new FileInteractionResolvedSurface(profileResolution, null);
        }

        return new FileInteractionResolvedSurface(
            profileResolution,
            composition.Renderers.Resolve(profileResolution.Profile!.Id, mode));
    }
}
