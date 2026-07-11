namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Tracks in-flight work and provides idempotent asynchronous session disposal.</summary>
internal sealed class FileBrowserSessionLifetime
{
    private readonly object sync = new();
    private readonly CancellationTokenSource cancellation = new();
    private int pendingExecutions;
    private bool disposalStarted;
    private TaskCompletionSource? executionsDrained;
    private Task? disposalTask;

    public bool IsDisposalStarted
    {
        get
        {
            lock (sync)
            {
                return disposalStarted;
            }
        }
    }

    public CancellationToken Begin()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposalStarted, this);
            pendingExecutions++;
            return cancellation.Token;
        }
    }

    public void End()
    {
        lock (sync)
        {
            pendingExecutions--;
            if (disposalStarted && pendingExecutions == 0)
            {
                executionsDrained?.TrySetResult();
            }
        }
    }

    public void ThrowIfDisposed(object owner)
        => ObjectDisposedException.ThrowIf(IsDisposalStarted, owner);

    public ValueTask DisposeAsync(
        Func<ValueTask> cancelExternal,
        Action cleanup)
    {
        ArgumentNullException.ThrowIfNull(cancelExternal);
        ArgumentNullException.ThrowIfNull(cleanup);
        lock (sync)
        {
            if (disposalTask is not null)
            {
                return new ValueTask(disposalTask);
            }

            disposalStarted = true;
            Task drain = pendingExecutions == 0
                ? Task.CompletedTask
                : (executionsDrained ??= new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            disposalTask = DisposeCoreAsync(drain, cancelExternal, cleanup);
            return new ValueTask(disposalTask);
        }
    }

    private async Task DisposeCoreAsync(
        Task drain,
        Func<ValueTask> cancelExternal,
        Action cleanup)
    {
        await cancellation.CancelAsync().ConfigureAwait(false);
        await cancelExternal().ConfigureAwait(false);
        await drain.ConfigureAwait(false);
        cleanup();
        cancellation.Dispose();
    }
}
