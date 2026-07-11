namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>
/// Owns serialized command execution, cancellation generations, retry checkpoints, and lifecycle
/// independently from the renderer-facing session facade.
/// </summary>
internal sealed class FileBrowserSessionExecutionCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly FileBrowserSessionLifetime lifetime = new();
    private readonly FileBrowserSourceRevisionGuard sourceRevisions = new();
    private readonly FileBrowserSessionRuntime runtime;
    private readonly Action<FileBrowserOperationKind, FileBrowserError?> publishSnapshot;
    private FileBrowserOperationKind operation;
    private FileBrowserError? error;
    private RetryCommand? retryCommand;

    public FileBrowserSessionExecutionCoordinator(
        FileBrowserSessionRuntime runtime,
        Action<FileBrowserOperationKind, FileBrowserError?> publishSnapshot)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.publishSnapshot = publishSnapshot ?? throw new ArgumentNullException(nameof(publishSnapshot));
    }

    public FileBrowserOperationKind Operation => operation;

    public FileBrowserError? Error => error;

    internal FileBrowserSourceRevisionGuard SourceRevisions => sourceRevisions;

    public ValueTask ExecuteAsync(
        FileBrowserOperationKind operationKind,
        Func<CancellationToken, ValueTask> action,
        CancellationToken cancellationToken)
        => ExecuteCommandAsync(new RetryCommand(operationKind, action), retryRequested: false, cancellationToken);

    public ValueTask RetryAsync(CancellationToken cancellationToken)
        => ExecuteCommandAsync(null, retryRequested: true, cancellationToken);

    public ValueTask<T> ExecuteSerializedAsync<T>(
        Func<CancellationToken, ValueTask<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteSerializedCoreAsync(action, cancellationToken);
    }

    public ValueTask ExecuteSupersedingAsync(
        FileBrowserOperationKind? busyOperation,
        Func<CancellationToken, ValueTask> action,
        bool retryOnFailure,
        bool clearRetryOnSuccess,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        FileBrowserSourceRevisionChange change = sourceRevisions.Supersede();
        RetryCommand? failedCommand = retryOnFailure && busyOperation.HasValue
            ? new RetryCommand(busyOperation.Value, action)
            : null;
        return ExecuteSupersedingCoreAsync(
            change,
            busyOperation,
            action,
            failedCommand,
            clearRetryOnSuccess,
            cancellationToken);
    }

    public void PublishCurrent()
    {
        ThrowIfDisposed();
        publishSnapshot(operation, error);
    }

    public void ThrowIfDisposed() => lifetime.ThrowIfDisposed(this);

    private async ValueTask ExecuteCommandAsync(
        RetryCommand? requestedCommand,
        bool retryRequested,
        CancellationToken cancellationToken)
    {
        ExecutionLease lease = BeginExecution();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.LifetimeToken,
            lease.SourceToken);
        bool gateHeld = false;
        SessionCheckpoint? checkpoint = null;
        RetryCommand? command = null;
        try
        {
            await operationGate.WaitAsync(linked.Token).ConfigureAwait(false);
            gateHeld = true;
            linked.Token.ThrowIfCancellationRequested();
            command = retryRequested ? retryCommand : requestedCommand;
            if (command is null)
            {
                return;
            }

            checkpoint = new SessionCheckpoint(runtime.Capture(), error, retryCommand);
            operation = command.OperationKind;
            error = null;
            Publish(lease.SourceGeneration);
            await command.Action(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            retryCommand = null;
            operation = FileBrowserOperationKind.Idle;
            Publish(lease.SourceGeneration);
        }
        catch (Exception exception)
        {
            if (checkpoint is not null)
            {
                RestoreCheckpoint(checkpoint);
            }

            operation = FileBrowserOperationKind.Idle;
            if (linked.IsCancellationRequested)
            {
                PublishIfCurrent(lease.SourceGeneration);
                throw CreateCanceled(exception, linked.Token);
            }

            error = ProjectError("The source could not complete the file browser request.", exception);
            retryCommand = command;
            Publish(lease.SourceGeneration);
        }
        finally
        {
            if (gateHeld)
            {
                operationGate.Release();
            }

            linked.Dispose();
            EndExecution(lease);
        }
    }

    private async ValueTask<T> ExecuteSerializedCoreAsync<T>(
        Func<CancellationToken, ValueTask<T>> action,
        CancellationToken cancellationToken)
    {
        ExecutionLease lease = BeginExecution();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.LifetimeToken,
            lease.SourceToken);
        bool gateHeld = false;
        try
        {
            await operationGate.WaitAsync(linked.Token).ConfigureAwait(false);
            gateHeld = true;
            T result = await action(linked.Token).ConfigureAwait(false);
            if (linked.IsCancellationRequested && result is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }

            linked.Token.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception exception) when (linked.IsCancellationRequested)
        {
            throw CreateCanceled(exception, linked.Token);
        }
        finally
        {
            if (gateHeld)
            {
                operationGate.Release();
            }

            linked.Dispose();
            EndExecution(lease);
        }
    }

    private async ValueTask ExecuteSupersedingCoreAsync(
        FileBrowserSourceRevisionChange change,
        FileBrowserOperationKind? busyOperation,
        Func<CancellationToken, ValueTask> action,
        RetryCommand? failedCommand,
        bool clearRetryOnSuccess,
        CancellationToken cancellationToken)
    {
        ExecutionLease lease;
        try
        {
            lease = BeginExecution(change.Generation);
        }
        catch
        {
            await sourceRevisions.CancelRetiredAsync(change).ConfigureAwait(false);
            throw;
        }

        using var gateCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lease.LifetimeToken,
            lease.SourceToken);
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.LifetimeToken,
            lease.SourceToken);
        Task gateWait = operationGate.WaitAsync(gateCancellation.Token);
        bool gateHeld = false;
        SessionCheckpoint? checkpoint = null;
        Exception? retirementError = null;
        try
        {
            try
            {
                await sourceRevisions.CancelRetiredAsync(change).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                retirementError = exception;
            }

            await gateWait.ConfigureAwait(false);
            gateHeld = true;
            if (retirementError is not null)
            {
                throw retirementError;
            }

            actionCancellation.Token.ThrowIfCancellationRequested();
            checkpoint = new SessionCheckpoint(runtime.Capture(), error, retryCommand);
            if (busyOperation.HasValue)
            {
                operation = busyOperation.Value;
                error = null;
                Publish(lease.SourceGeneration);
            }

            await action(actionCancellation.Token).ConfigureAwait(false);
            actionCancellation.Token.ThrowIfCancellationRequested();
            if (clearRetryOnSuccess)
            {
                retryCommand = null;
            }

            operation = FileBrowserOperationKind.Idle;
            Publish(lease.SourceGeneration);
        }
        catch (Exception exception)
        {
            if (!gateHeld)
            {
                throw CreateCanceled(exception, gateCancellation.Token);
            }

            if (checkpoint is not null)
            {
                RestoreCheckpoint(checkpoint);
            }

            operation = FileBrowserOperationKind.Idle;
            if (actionCancellation.IsCancellationRequested || gateCancellation.IsCancellationRequested)
            {
                PublishIfCurrent(lease.SourceGeneration);
                throw CreateCanceled(exception, actionCancellation.IsCancellationRequested
                    ? actionCancellation.Token
                    : gateCancellation.Token);
            }

            error = ProjectError(
                busyOperation.HasValue
                    ? "The updated source state could not be initialized."
                    : "The file browser state could not be invalidated.",
                exception);
            if (failedCommand is not null)
            {
                retryCommand = failedCommand;
            }

            Publish(lease.SourceGeneration);
        }
        finally
        {
            if (gateHeld)
            {
                operationGate.Release();
            }

            gateCancellation.Dispose();
            actionCancellation.Dispose();
            EndExecution(lease);
        }
    }

    private ExecutionLease BeginExecution(long? expectedSourceGeneration = null)
    {
        FileBrowserSourceRevision source = sourceRevisions.Capture(expectedSourceGeneration);
        try
        {
            return new ExecutionLease(lifetime.Begin(), source);
        }
        catch
        {
            sourceRevisions.Release(source);
            throw;
        }
    }

    private void EndExecution(ExecutionLease lease)
    {
        sourceRevisions.Release(lease.Source);
        lifetime.End();
    }

    private void RestoreCheckpoint(SessionCheckpoint checkpoint)
    {
        runtime.Restore(checkpoint.Runtime);
        error = checkpoint.Error;
        retryCommand = checkpoint.RetryCommand;
    }

    private void Publish(long expectedSourceGeneration)
    {
        if (!lifetime.IsDisposalStarted && sourceRevisions.IsCurrent(expectedSourceGeneration))
        {
            publishSnapshot(operation, error);
        }
    }

    private void PublishIfCurrent(long expectedSourceGeneration)
    {
        if (sourceRevisions.IsCurrent(expectedSourceGeneration))
        {
            Publish(expectedSourceGeneration);
        }
    }

    private static FileBrowserError ProjectError(string message, Exception exception)
        => exception is FileBrowserProviderException providerException
            ? providerException.Error
            : FileBrowserSessionErrors.ProviderFailure(message, exception);

    private static OperationCanceledException CreateCanceled(Exception exception, CancellationToken token)
        => new("The file browser operation was canceled.", exception, token);

    public ValueTask DisposeAsync()
        => lifetime.DisposeAsync(
            sourceRevisions.CancelCurrentAsync,
            () =>
            {
                operationGate.Dispose();
                sourceRevisions.Dispose();
            });
}
