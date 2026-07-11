namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Owns content edit, history, persistence, and optional preview state for one resolved profile.</summary>
internal sealed class FileInteractionEditingRuntime : IAsyncDisposable
{
    private readonly FileInteractionEditCoordinator coordinator;
    private readonly int maximumContentBytes;
    private FileInteractionTextBuffer? textBuffer;
    private byte[] content;
    private bool disposed;

    private FileInteractionEditingRuntime(
        FileInteractionProfileDescriptor profile,
        FileInteractionContentKind contentKind,
        FileInteractionEditCoordinator coordinator,
        int maximumContentBytes,
        FileInteractionTextBuffer? textBuffer,
        byte[] content,
        FileInteractionPreviewRuntime? preview)
    {
        Profile = profile;
        ContentKind = contentKind;
        this.coordinator = coordinator;
        this.maximumContentBytes = maximumContentBytes;
        this.textBuffer = textBuffer;
        this.content = content;
        Preview = preview;
    }

    public FileInteractionProfileDescriptor Profile { get; }

    public FileInteractionContentKind ContentKind { get; }

    public FileInteractionPreviewRuntime? Preview { get; }

    public ReadOnlyMemory<byte> Content => content;

    public string? Text => textBuffer?.Text;

    public string? EncodingName => textBuffer?.EncodingName;

    public FileEditSessionState State => coordinator.State;

    public FileEditHistoryState HistoryState => coordinator.HistoryState;

    /// <summary>Raised after persistence has updated this runtime's immutable edit-session state.</summary>
    public event EventHandler<FileSaveCompletedEventArgs>? SaveCompleted;

    public bool CanSave => Supports(FileInteractionCapabilities.Save);

    public bool CanUndo => Supports(FileInteractionCapabilities.Undo) && HistoryState.CanUndo;

    public bool CanRedo => Supports(FileInteractionCapabilities.Redo) && HistoryState.CanRedo;

    public static async ValueTask<FileInteractionEditingRuntime> CreateAsync(
        FileInteractionProfileDescriptor profile,
        FileInteractionContentKind contentKind,
        FileInteractionRequest request,
        FileInteractionComponentComposition composition,
        ReadOnlyMemory<byte> initialContent,
        int maximumContentBytes,
        IFileSaveTarget saveTarget,
        Func<bool> canAutoSave,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Enum.IsDefined(contentKind))
        {
            throw new ArgumentOutOfRangeException(nameof(contentKind));
        }

        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(saveTarget);
        ArgumentNullException.ThrowIfNull(canAutoSave);
        if (maximumContentBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumContentBytes));
        }

        var bytes = initialContent.ToArray();
        var textBuffer = contentKind == FileInteractionContentKind.Text
            ? FileInteractionTextBuffer.Decode(bytes)
            : null;
        var snapshot = new FileEditSnapshot(
            request.File,
            editRevision: 0,
            bytes,
            request.MediaType,
            textBuffer?.EncodingName);
        var history = await composition.Core.HistoryProviders.CreateAsync(
            profile, request, cancellationToken).ConfigureAwait(false);
        var coordinator = await FileInteractionEditCoordinator.CreateAsync(
            snapshot,
            request.ContentRevision,
            saveTarget,
            history,
            profile.Capabilities.HasFlag(FileInteractionCapabilities.Save)
                ? profile.AutoSave
                : FileAutoSaveOptions.Disabled,
            cancellationToken: cancellationToken,
            canAutoSave: canAutoSave).ConfigureAwait(false);
        try
        {
            var previewRenderer = profile.Preview.Enabled
                ? composition.Renderers.Resolve(profile.Id, FileInteractionMode.View).Renderer
                : null;
            var preview = previewRenderer is null
                ? null
                : new FileInteractionPreviewRuntime(
                    previewRenderer,
                    profile.Preview,
                    bytes,
                    textBuffer?.Text,
                    snapshot.MediaType,
                    snapshot.EncodingName);
            var runtime = new FileInteractionEditingRuntime(
                profile,
                contentKind,
                coordinator,
                maximumContentBytes,
                textBuffer,
                bytes,
                preview);
            coordinator.SaveCompleted += runtime.OnSaveCompleted;
            return runtime;
        }
        catch
        {
            await coordinator.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask ApplyTextAsync(
        string text,
        string? mediaType,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        if (textBuffer is null)
        {
            throw new InvalidOperationException("This editor does not expose a text surface.");
        }

        var changedTextUnits = FileInteractionTextChangeCounter.CountChangedTextUnits(
            textBuffer.Text,
            text);
        var encoded = textBuffer.Encode(text);
        EnsureWithinLimit(encoded.Length);
        var snapshot = await coordinator.ApplyEditAsync(
            encoded,
            mediaType,
            textBuffer.EncodingName,
            cancellationToken,
            changedTextUnits).ConfigureAwait(false);
        Apply(snapshot);
    }

    public async ValueTask ApplyContentAsync(
        FileInteractionContentChange change,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(change);
        EnsureWithinLimit(change.Content.Length);
        var nextText = ContentKind == FileInteractionContentKind.Text
            ? FileInteractionTextBuffer.Decode(change.Content)
            : null;
        var changedTextUnits = textBuffer is not null && nextText is not null
            ? FileInteractionTextChangeCounter.CountChangedTextUnits(textBuffer.Text, nextText.Text)
            : 0;
        var snapshot = await coordinator.ApplyEditAsync(
            change.Content,
            change.MediaType,
            change.EncodingName ?? nextText?.EncodingName,
            cancellationToken,
            changedTextUnits).ConfigureAwait(false);
        Apply(snapshot);
    }

    public void NotifySaveAvailabilityChanged()
        => coordinator.NotifyAutoSaveAvailabilityChanged();

    public ValueTask<bool> UndoAsync(CancellationToken cancellationToken = default)
        => Supports(FileInteractionCapabilities.Undo)
            ? ApplyHistoryAsync(isUndo: true, cancellationToken)
            : ValueTask.FromResult(false);

    public ValueTask<bool> RedoAsync(CancellationToken cancellationToken = default)
        => Supports(FileInteractionCapabilities.Redo)
            ? ApplyHistoryAsync(isUndo: false, cancellationToken)
            : ValueTask.FromResult(false);

    public ValueTask<FileSaveOperationResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return coordinator.SaveNowAsync(cancellationToken);
    }

    public ValueTask<FileEditSessionState> ResolveConflictByRebasingAsync(
        FileContentRevision revision,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return coordinator.ResolveConflictByRebasingAsync(revision, cancellationToken);
    }

    public ValueTask<FileEditSessionState> ResolveConflictByOverwriteAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return coordinator.ResolveConflictByOverwriteAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (Preview is not null)
        {
            await Preview.DisposeAsync().ConfigureAwait(false);
        }

        coordinator.SaveCompleted -= OnSaveCompleted;
        await coordinator.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> ApplyHistoryAsync(
        bool isUndo,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var snapshot = isUndo
            ? await coordinator.UndoAsync(cancellationToken).ConfigureAwait(false)
            : await coordinator.RedoAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return false;
        }

        Apply(snapshot);
        return true;
    }

    private void Apply(FileEditSnapshot snapshot)
    {
        content = snapshot.Content.ToArray();
        textBuffer = ContentKind == FileInteractionContentKind.Text
            ? FileInteractionTextBuffer.Decode(content)
            : null;
        Preview?.Request(snapshot);
    }

    private bool Supports(FileInteractionCapabilities capability)
        => Profile.Capabilities.HasFlag(capability);

    private void OnSaveCompleted(object? sender, FileSaveCompletedEventArgs args)
        => SaveCompleted?.Invoke(this, args);

    private void EnsureWithinLimit(int contentLength)
    {
        if (contentLength > maximumContentBytes)
        {
            throw new FileInteractionContentTooLargeException(maximumContentBytes);
        }
    }
}
