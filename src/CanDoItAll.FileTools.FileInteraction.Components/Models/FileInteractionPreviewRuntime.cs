namespace CanDoItAll.FileTools.FileInteraction.Components;

internal readonly record struct FileInteractionPreviewState(
    ReadOnlyMemory<byte> Content,
    string? Text,
    string? MediaType,
    string? EncodingName,
    long EditRevision,
    bool IsPending);

/// <summary>Owns one edit surface's debounced preview lifecycle and stale-result suppression.</summary>
internal sealed class FileInteractionPreviewRuntime : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly FilePreviewCoordinator<ReadOnlyMemory<byte>> coordinator;
    private readonly HashSet<Task> observers = [];
    private ReadOnlyMemory<byte> content;
    private string? text;
    private string? mediaType;
    private string? encodingName;
    private long editRevision;
    private bool pending;
    private bool disposed;

    public FileInteractionPreviewRuntime(
        FileInteractionRendererDescriptor renderer,
        FilePreviewOptions options,
        ReadOnlyMemory<byte> initialContent,
        string? initialText,
        string? initialMediaType = null,
        string? initialEncodingName = null)
    {
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        ArgumentNullException.ThrowIfNull(options);
        coordinator = new FilePreviewCoordinator<ReadOnlyMemory<byte>>(
            new IdentityFilePreviewGenerator(), options.Debounce);
        content = initialContent;
        text = initialText;
        mediaType = initialMediaType;
        encodingName = initialEncodingName;
        ShowByDefault = options.SplitByDefault;
    }

    public event EventHandler? Changed;

    public event EventHandler<FileInteractionPreviewFaultEventArgs>? Faulted;

    public FileInteractionRendererDescriptor Renderer { get; }

    public bool ShowByDefault { get; }

    public ReadOnlyMemory<byte> Content
    {
        get { lock (gate) { return content; } }
    }

    public string? Text
    {
        get { lock (gate) { return text; } }
    }

    public string? MediaType
    {
        get { lock (gate) { return mediaType; } }
    }

    public string? EncodingName
    {
        get { lock (gate) { return encodingName; } }
    }

    public long EditRevision
    {
        get { lock (gate) { return editRevision; } }
    }

    public bool IsPending
    {
        get { lock (gate) { return pending; } }
    }

    public FileInteractionPreviewState Snapshot
    {
        get
        {
            lock (gate)
            {
                return new FileInteractionPreviewState(
                    content,
                    text,
                    mediaType,
                    encodingName,
                    editRevision,
                    pending);
            }
        }
    }

    public void Request(FileEditSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Task observer;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            pending = true;
            observers.RemoveWhere(task => task.IsCompleted);
            observer = ObserveAsync(coordinator.RequestAsync(snapshot).AsTask());
            observers.Add(observer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task[] active;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            active = observers.ToArray();
        }

        await coordinator.DisposeAsync().ConfigureAwait(false);
        await Task.WhenAll(active).ConfigureAwait(false);
        lock (gate)
        {
            observers.Clear();
        }
    }

    private async Task ObserveAsync(Task<FilePreviewUpdate<ReadOnlyMemory<byte>>?> request)
    {
        try
        {
            var update = await request.ConfigureAwait(false);
            if (update is null)
            {
                return;
            }

            EventHandler? changed;
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                content = update.Preview;
                text = Renderer.ContentKind == FileInteractionContentKind.Text
                    ? FileInteractionTextBuffer.Decode(update.Preview).Text
                    : null;
                mediaType = update.MediaType;
                encodingName = update.EncodingName;
                editRevision = update.EditRevision;
                pending = false;
                changed = Changed;
            }

            changed?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            EventHandler<FileInteractionPreviewFaultEventArgs>? faulted;
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                pending = false;
                faulted = Faulted;
            }

            faulted?.Invoke(this, new FileInteractionPreviewFaultEventArgs(exception));
        }
    }
}

internal sealed class FileInteractionPreviewFaultEventArgs(Exception error) : EventArgs
{
    public Exception Error { get; } = error ?? throw new ArgumentNullException(nameof(error));
}
