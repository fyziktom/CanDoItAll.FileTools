namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>
/// Coordinates one edit session with optional file-type history. It is the UI-neutral facade used by
/// component shells so undo/redo applications receive new monotonic edit revisions without corrupting
/// the history branch.
/// </summary>
public sealed class FileInteractionEditCoordinator : IAsyncDisposable
{
    private readonly FileSaveCoordinator saves;
    private readonly IFileEditHistoryProvider? history;
    private readonly SemaphoreSlim editGate = new(1, 1);
    private int disposed;

    private FileInteractionEditCoordinator(
        FileSaveCoordinator saves,
        IFileEditHistoryProvider? history)
    {
        this.saves = saves;
        this.history = history;
    }

    /// <summary>
    /// Creates a coordinator and transfers ownership of <paramref name="historyProvider"/> to it.
    /// </summary>
    public static async ValueTask<FileInteractionEditCoordinator> CreateAsync(
        FileEditSnapshot initialSnapshot,
        FileContentRevision? baseRevision,
        IFileSaveTarget saveTarget,
        IFileEditHistoryProvider? historyProvider = null,
        FileAutoSaveOptions? autoSave = null,
        IFileInteractionDelay? delay = null,
        CancellationToken cancellationToken = default,
        Func<bool>? canAutoSave = null)
    {
        ArgumentNullException.ThrowIfNull(initialSnapshot);
        ArgumentNullException.ThrowIfNull(saveTarget);
        cancellationToken.ThrowIfCancellationRequested();

        var saves = new FileSaveCoordinator(
            new FileEditSession(initialSnapshot, baseRevision),
            saveTarget,
            autoSave,
            delay,
            canAutoSave);
        try
        {
            if (historyProvider is not null)
            {
                await historyProvider.ResetAsync(
                    initialSnapshot.File,
                    baseRevision,
                    initialSnapshot,
                    cancellationToken).ConfigureAwait(false);
            }

            return new FileInteractionEditCoordinator(saves, historyProvider);
        }
        catch
        {
            await saves.DisposeAsync().ConfigureAwait(false);
            if (historyProvider is not null)
            {
                await historyProvider.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    public FileEditSessionState State => saves.State;

    public FileEditHistoryState HistoryState => history?.State ?? default;

    /// <summary>Forwards post-transition persistence notifications from the save coordinator.</summary>
    public event EventHandler<FileSaveCompletedEventArgs> SaveCompleted
    {
        add
        {
            ThrowIfDisposed();
            saves.SaveCompleted += value;
        }
        remove => saves.SaveCompleted -= value;
    }

    public async ValueTask<FileEditSnapshot> ApplyEditAsync(
        ReadOnlyMemory<byte> content,
        string? mediaType = null,
        string? encodingName = null,
        CancellationToken cancellationToken = default,
        int changedTextUnits = 0)
    {
        await EnterEditGateAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = saves.ApplyEdit(content, mediaType, encodingName, changedTextUnits);
            if (history is not null)
            {
                await history.RecordAsync(snapshot, cancellationToken).ConfigureAwait(false);
            }

            return snapshot;
        }
        finally
        {
            editGate.Release();
        }
    }

    public ValueTask<FileEditSnapshot?> UndoAsync(CancellationToken cancellationToken = default)
        => ApplyHistoryAsync(isUndo: true, cancellationToken);

    public ValueTask<FileEditSnapshot?> RedoAsync(CancellationToken cancellationToken = default)
        => ApplyHistoryAsync(isUndo: false, cancellationToken);

    public ValueTask<FileSaveOperationResult> SaveNowAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return saves.SaveNowAsync(cancellationToken);
    }

    public ValueTask WaitForPendingSavesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return saves.WaitForPendingSavesAsync(cancellationToken);
    }

    public void NotifyAutoSaveAvailabilityChanged()
    {
        ThrowIfDisposed();
        saves.NotifyAutoSaveAvailabilityChanged();
    }

    /// <summary>
    /// Updates only the expected persisted revision and resets local history before retry. It does not
    /// merge external content into the current local snapshot.
    /// </summary>
    public async ValueTask<FileEditSessionState> ResolveConflictByRebasingAsync(
        FileContentRevision actualRevision,
        CancellationToken cancellationToken = default)
    {
        await EnterEditGateAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = saves.ResolveConflictByRebasing(actualRevision);
            await ResetHistoryAsync(state, cancellationToken).ConfigureAwait(false);
            return state;
        }
        finally
        {
            editGate.Release();
        }
    }

    public async ValueTask<FileEditSessionState> ResolveConflictByOverwriteAsync(
        CancellationToken cancellationToken = default)
    {
        await EnterEditGateAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = saves.ResolveConflictByOverwrite();
            await ResetHistoryAsync(state, cancellationToken).ConfigureAwait(false);
            return state;
        }
        finally
        {
            editGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await editGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await saves.DisposeAsync().ConfigureAwait(false);
            if (history is not null)
            {
                await history.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            editGate.Release();
            editGate.Dispose();
        }
    }

    private async ValueTask<FileEditSnapshot?> ApplyHistoryAsync(
        bool isUndo,
        CancellationToken cancellationToken)
    {
        await EnterEditGateAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (history is null)
            {
                return null;
            }

            if (saves.State.EditRevision == long.MaxValue)
            {
                throw new OverflowException("The edit revision cannot advance further.");
            }

            var historical = isUndo
                ? await history.UndoAsync(cancellationToken).ConfigureAwait(false)
                : await history.RedoAsync(cancellationToken).ConfigureAwait(false);
            return historical is null
                ? null
                : saves.ApplyEdit(historical.Content, historical.MediaType, historical.EncodingName);
        }
        finally
        {
            editGate.Release();
        }
    }

    private async ValueTask ResetHistoryAsync(
        FileEditSessionState state,
        CancellationToken cancellationToken)
    {
        if (history is not null)
        {
            await history.ResetAsync(
                state.Current.File,
                state.BaseRevision,
                state.Current,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask EnterEditGateAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await editGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (Volatile.Read(ref disposed) != 0)
        {
            editGate.Release();
            ThrowIfDisposed();
        }
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
}
