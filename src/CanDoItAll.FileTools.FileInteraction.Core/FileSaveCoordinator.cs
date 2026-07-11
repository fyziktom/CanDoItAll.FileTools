namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>
/// Coordinates manual and automatic persistence with one active save and latest-edit coalescing.
/// </summary>
public sealed class FileSaveCoordinator : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly FileEditSession session;
    private readonly IFileSaveTarget target;
    private readonly FileAutoSaveScheduler autoSaveScheduler;
    private readonly FileSaveCompletionPublisher completionPublisher = new();
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private TaskCompletionSource<FileSaveOperationResult>? runnerCompletion;
    private bool runnerActive;
    private bool saveRequested;
    private bool requestedSaveIsAutomatic = true;
    private bool disposed;

    public FileSaveCoordinator(
        FileEditSession session,
        IFileSaveTarget target,
        FileAutoSaveOptions? options = null,
        IFileInteractionDelay? delay = null,
        Func<bool>? canAutoSave = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.target = target ?? throw new ArgumentNullException(nameof(target));
        autoSaveScheduler = new FileAutoSaveScheduler(
            options ?? FileAutoSaveOptions.Disabled,
            delay ?? SystemFileInteractionDelay.Instance,
            () => RequestSave(isAutomatic: true),
            canAutoSave);
    }

    /// <summary>
    /// Raised after an actual save attempt has been acknowledged, rejected, or cancelled by the edit
    /// session. Observer failures are isolated from persistence and do not change the save result.
    /// </summary>
    public event EventHandler<FileSaveCompletedEventArgs> SaveCompleted
    {
        add => completionPublisher.Completed += value;
        remove => completionPublisher.Completed -= value;
    }

    public FileEditSessionState State => session.State;

    public FileEditSnapshot ApplyEdit(
        ReadOnlyMemory<byte> content,
        string? mediaType = null,
        string? encodingName = null,
        int changedTextUnits = 0)
    {
        if (changedTextUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedTextUnits));
        }

        lock (gate)
        {
            ThrowIfDisposedLocked();
            var snapshot = session.ApplyEdit(content, mediaType, encodingName);
            autoSaveScheduler.NotifyChanged(changedTextUnits);
            return snapshot;
        }
    }

    /// <summary>
    /// Clears a conflict by accepting the supplied persisted revision as the optimistic-concurrency
    /// base for the next save. This does not merge external content; the current local snapshot remains dirty.
    /// </summary>
    public FileEditSessionState ResolveConflictByRebasing(FileContentRevision actualRevision)
    {
        if (string.IsNullOrWhiteSpace(actualRevision.Value))
        {
            throw new ArgumentException("A valid persisted revision is required.", nameof(actualRevision));
        }

        lock (gate)
        {
            ThrowIfDisposedLocked();
            return session.ResolveConflict(actualRevision);
        }
    }

    /// <summary>
    /// Clears a conflict and deliberately removes the optimistic-concurrency expectation for the next save.
    /// The host remains authoritative over whether an overwrite is allowed.
    /// </summary>
    public FileEditSessionState ResolveConflictByOverwrite()
    {
        lock (gate)
        {
            ThrowIfDisposedLocked();
            return session.ResolveConflict(expectedRevision: null);
        }
    }

    public async ValueTask<FileSaveOperationResult> SaveNowAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var task = RequestSave(isAutomatic: false);
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask WaitForPendingSavesAsync(CancellationToken cancellationToken = default)
    {
        Task? task;
        lock (gate)
        {
            task = runnerCompletion?.Task;
        }

        if (task is not null)
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void NotifyAutoSaveAvailabilityChanged()
    {
        ThrowIfDisposed();
        autoSaveScheduler.NotifyAvailabilityChanged();
    }

    public async ValueTask DisposeAsync()
    {
        Task? runner;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            saveRequested = false;
            requestedSaveIsAutomatic = true;
            runner = runnerCompletion?.Task;
        }

        lifetimeCancellation.Cancel();
        await autoSaveScheduler.DisposeAsync().ConfigureAwait(false);
        if (runner is not null)
        {
            await IgnoreCancellationAsync(runner).ConfigureAwait(false);
        }

        lifetimeCancellation.Dispose();
    }

    private Task<FileSaveOperationResult> RequestSave(bool isAutomatic)
    {
        lock (gate)
        {
            if (disposed)
            {
                return Task.FromResult(new FileSaveOperationResult(FileSaveOperationStatus.Cancelled));
            }

            var state = session.State;
            if (state.HasConflict)
            {
                return Task.FromResult(new FileSaveOperationResult(
                    FileSaveOperationStatus.Conflict,
                    state.EditRevision,
                    Error: state.LastSaveError));
            }

            if (!state.IsDirty && !runnerActive)
            {
                return Task.FromResult(new FileSaveOperationResult(FileSaveOperationStatus.NotDirty));
            }

            if (!saveRequested)
            {
                requestedSaveIsAutomatic = isAutomatic;
            }
            else
            {
                requestedSaveIsAutomatic &= isAutomatic;
            }

            saveRequested = true;
            TaskCompletionSource<FileSaveOperationResult> completion;
            if (!runnerActive)
            {
                runnerActive = true;
                completion = new TaskCompletionSource<FileSaveOperationResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                runnerCompletion = completion;
                Observe(RunSaveLoopAsync(completion));
            }
            else
            {
                completion = runnerCompletion!;
            }

            return completion.Task;
        }
    }

    private async Task RunSaveLoopAsync(TaskCompletionSource<FileSaveOperationResult> completion)
    {
        var lastResult = new FileSaveOperationResult(FileSaveOperationStatus.NotDirty);
        while (true)
        {
            bool isAutomatic;
            lock (gate)
            {
                if (disposed)
                {
                    saveRequested = false;
                    CompleteRunnerLocked(completion, lastResult);
                    return;
                }

                if (!saveRequested)
                {
                    CompleteRunnerLocked(completion, lastResult);
                    return;
                }

                saveRequested = false;
                isAutomatic = requestedSaveIsAutomatic;
                requestedSaveIsAutomatic = true;
            }

            if (lifetimeCancellation.IsCancellationRequested)
            {
                lastResult = new FileSaveOperationResult(
                    FileSaveOperationStatus.Cancelled,
                    session.State.EditRevision);
                continue;
            }

            var request = session.TryBeginSave(isAutomatic);
            if (request is null)
            {
                continue;
            }

            if (lifetimeCancellation.IsCancellationRequested)
            {
                session.CancelSave(request.EditRevision);
                lastResult = new FileSaveOperationResult(
                    FileSaveOperationStatus.Cancelled,
                    request.EditRevision);
                continue;
            }

            try
            {
                var targetResult = await target.SaveAsync(request, lifetimeCancellation.Token).ConfigureAwait(false);
                session.AcknowledgeSave(request.EditRevision, targetResult.PersistedRevision);
                lastResult = new FileSaveOperationResult(
                    FileSaveOperationStatus.Saved,
                    request.EditRevision,
                    targetResult.PersistedRevision);
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                session.CancelSave(request.EditRevision);
                lastResult = new FileSaveOperationResult(
                    FileSaveOperationStatus.Cancelled,
                    request.EditRevision);
            }
            catch (FileSaveConflictException exception)
            {
                session.RejectSave(request.EditRevision, exception);
                lastResult = new FileSaveOperationResult(
                    FileSaveOperationStatus.Conflict,
                    request.EditRevision,
                    Error: exception);
            }
            catch (Exception exception)
            {
                session.RejectSave(request.EditRevision, exception);
                lastResult = new FileSaveOperationResult(
                    FileSaveOperationStatus.Failed,
                    request.EditRevision,
                    Error: exception);
            }

            completionPublisher.Publish(this, lastResult, session.State);
        }
    }

    private void CompleteRunnerLocked(
        TaskCompletionSource<FileSaveOperationResult> completion,
        FileSaveOperationResult result)
    {
        runnerActive = false;
        if (ReferenceEquals(runnerCompletion, completion))
        {
            runnerCompletion = null;
        }

        completion.TrySetResult(result);
    }

    private static void Observe(Task task)
    {
        _ = ObserveCoreAsync(task);

        static async Task ObserveCoreAsync(Task observed)
        {
            try
            {
                await observed.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        lock (gate)
        {
            ThrowIfDisposedLocked();
        }
    }

    private void ThrowIfDisposedLocked()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
