namespace CanDoItAll.FileTools.FileBrowser.Components;

/// <summary>Owns the single cancellable delay used by controlled search input.</summary>
internal sealed class FileBrowserSearchDebouncer : IAsyncDisposable
{
    private readonly Lock gate = new();
    private CancellationTokenSource? pending;
    private long version;
    private bool disposed;

    public bool HasPending
    {
        get
        {
            lock (gate)
            {
                return pending is not null;
            }
        }
    }

    public async ValueTask ScheduleAsync(
        TimeSpan delay,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        CancellationTokenSource current;
        CancellationTokenSource? previous;
        long currentVersion;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            previous = pending;
            current = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pending = current;
            currentVersion = ++version;
        }

        if (previous is not null)
        {
            await previous.CancelAsync();
        }

        try
        {
            await Task.Delay(delay, current.Token);
            current.Token.ThrowIfCancellationRequested();
            await callback(current.Token);
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
            // A later edit, session replacement, or component disposal superseded this work.
        }
        finally
        {
            lock (gate)
            {
                if (version == currentVersion && ReferenceEquals(pending, current))
                {
                    pending = null;
                }
            }

            current.Dispose();
        }
    }

    public async ValueTask CancelAsync()
    {
        CancellationTokenSource? current;
        lock (gate)
        {
            current = pending;
            pending = null;
            version++;
        }

        if (current is not null)
        {
            await current.CancelAsync();
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? current;
        lock (gate)
        {
            current = pending;
            pending = null;
            version++;
        }

        current?.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        await CancelAsync();
    }
}
