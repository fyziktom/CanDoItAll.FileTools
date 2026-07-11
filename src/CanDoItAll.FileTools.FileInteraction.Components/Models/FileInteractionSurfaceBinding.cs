namespace CanDoItAll.FileTools.FileInteraction.Components;

internal readonly record struct FileInteractionSurfaceLoadOutcome(bool IsCurrent, Exception? Error = null);

/// <summary>Owns bounded load, metadata-aware resolution, and view-mode state for one file surface.</summary>
internal sealed class FileInteractionSurfaceBinding : IAsyncDisposable
{
    private readonly FileInteractionSurfaceLoader loader = new();
    private CancellationTokenSource? loadCancellation;
    private FileInteractionInputStamp? inputStamp;
    private FileInteractionTextBuffer? textBuffer;
    private byte[] content = [];
    private bool disposed;

    public FileInteractionComponentComposition Composition { get; private set; } =
        FileInteractionComponentComposition.BuiltIn;

    public FileInteractionRequest? Request { get; private set; }

    public FileInteractionProfileDescriptor? Profile { get; private set; }

    public FileInteractionRendererDescriptor? Renderer { get; private set; }

    public ReadOnlyMemory<byte> Content => content;

    public string? Text => textBuffer?.Text;

    public string? EncodingName => textBuffer?.EncodingName;

    public FileInteractionMode Mode { get; private set; }

    public FileInteractionLoadState State { get; private set; }

    public string ResolutionMessage { get; private set; } =
        "No compatible profile and renderer were registered.";

    public string ErrorMessage { get; private set; } =
        "The content source did not provide a readable file.";

    public bool HasInputChanged(
        FileInteractionRequest request,
        IFileContentSource source,
        FileInteractionComponentComposition composition,
        int maximumBytes)
    {
        var next = new FileInteractionInputStamp(request, source, composition, maximumBytes);
        return inputStamp is null || !inputStamp.Matches(next);
    }

    public void CancelLoad() => loadCancellation?.Cancel();

    public async ValueTask<FileInteractionSurfaceLoadOutcome> LoadAsync(
        FileInteractionRequest request,
        IFileContentSource source,
        FileInteractionComponentComposition composition,
        int maximumBytes,
        Func<bool> isCurrent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(isCurrent);
        ObjectDisposedException.ThrowIf(disposed, this);
        CancelLoad();
        inputStamp = new FileInteractionInputStamp(request, source, composition, maximumBytes);
        Composition = composition;
        Request = request;
        Mode = request.Mode;
        Profile = null;
        Renderer = null;
        content = [];
        textBuffer = null;
        State = FileInteractionLoadState.Loading;
        ResolutionMessage = "No compatible profile and renderer were registered.";
        ErrorMessage = "The content source did not provide a readable file.";

        var cancellation = new CancellationTokenSource();
        loadCancellation = cancellation;
        try
        {
            var loaded = await loader.LoadAsync(
                request,
                source,
                composition,
                Mode,
                maximumBytes,
                cancellation.Token).ConfigureAwait(false);
            if (!isCurrent())
            {
                return default;
            }

            Request = loaded.Request;
            if (!Apply(loaded.Surface))
            {
                State = FileInteractionLoadState.Unsupported;
                return new FileInteractionSurfaceLoadOutcome(true);
            }

            content = loaded.Content.ToArray();
            DecodeTextIfNeeded(Renderer!);
            State = FileInteractionLoadState.Loaded;
            return new FileInteractionSurfaceLoadOutcome(true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return default;
        }
        catch (Exception exception)
        {
            if (!isCurrent())
            {
                return default;
            }

            State = FileInteractionLoadState.Error;
            ErrorMessage = exception is FileInteractionContentTooLargeException
                ? exception.Message
                : "The file could not be read or decoded by this interaction surface.";
            return new FileInteractionSurfaceLoadOutcome(true, exception);
        }
        finally
        {
            if (ReferenceEquals(loadCancellation, cancellation))
            {
                loadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    public bool TrySetMode(FileInteractionMode mode)
    {
        if (State != FileInteractionLoadState.Loaded || Request is null)
        {
            return false;
        }

        var resolved = FileInteractionSurfaceResolver.Resolve(Composition, Request, mode);
        if (!resolved.IsResolved)
        {
            return false;
        }

        Mode = mode;
        Request = FileInteractionRequestFactory.WithMode(Request, mode);
        Apply(resolved);
        DecodeTextIfNeeded(Renderer!);
        return true;
    }

    public bool CanUseMode(FileInteractionMode mode)
        => State == FileInteractionLoadState.Loaded
            && Request is not null
            && FileInteractionSurfaceResolver.Resolve(Composition, Request, mode).IsResolved;

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            CancelLoad();
            loadCancellation = null;
        }

        return ValueTask.CompletedTask;
    }

    private bool Apply(FileInteractionResolvedSurface surface)
    {
        if (!surface.IsResolved)
        {
            Profile = null;
            Renderer = null;
            ResolutionMessage = surface.Message;
            return false;
        }

        Profile = surface.ProfileResolution.Profile;
        Renderer = surface.RendererResolution!.Renderer;
        ResolutionMessage = string.Empty;
        return true;
    }

    private void DecodeTextIfNeeded(FileInteractionRendererDescriptor renderer)
        => textBuffer = renderer.ContentKind == FileInteractionContentKind.Text
            ? FileInteractionTextBuffer.Decode(content)
            : null;
}
