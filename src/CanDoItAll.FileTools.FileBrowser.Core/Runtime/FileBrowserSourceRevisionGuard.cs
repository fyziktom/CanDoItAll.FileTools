namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>
/// Rotates source-state generations and keeps retired cancellation sources alive only while an
/// operation still depends on their token.
/// </summary>
internal sealed class FileBrowserSourceRevisionGuard : IDisposable
{
    private readonly object sync = new();
    private readonly HashSet<FileBrowserSourceGeneration> generations = [];
    private FileBrowserSourceGeneration current;
    private long generation;
    private int disposedGenerationCount;
    private bool disposed;

    public FileBrowserSourceRevisionGuard()
    {
        current = new FileBrowserSourceGeneration(0);
        generations.Add(current);
    }

    internal int RetiredGenerationCount
    {
        get
        {
            lock (sync)
            {
                return generations.Count(state => state.IsRetired);
            }
        }
    }

    internal int DisposedGenerationCount
    {
        get
        {
            lock (sync)
            {
                return disposedGenerationCount;
            }
        }
    }

    public FileBrowserSourceRevision Capture(long? expectedGeneration = null)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (expectedGeneration.HasValue && generation != expectedGeneration.Value)
            {
                throw new OperationCanceledException("The source-state update was superseded.");
            }

            current.ActiveLeaseCount++;
            return new FileBrowserSourceRevision(current);
        }
    }

    public FileBrowserSourceRevisionChange Supersede()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            FileBrowserSourceGeneration previous = current;
            previous.IsRetired = true;
            current = new FileBrowserSourceGeneration(++generation);
            generations.Add(current);
            return new FileBrowserSourceRevisionChange(current.Generation, previous);
        }
    }

    public bool IsCurrent(long expectedGeneration)
    {
        lock (sync)
        {
            return !disposed && generation == expectedGeneration;
        }
    }

    public async ValueTask CancelRetiredAsync(FileBrowserSourceRevisionChange change)
    {
        try
        {
            await change.Previous.Cancellation.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            lock (sync)
            {
                change.Previous.CancellationCompleted = true;
                TryDisposeRetired(change.Previous);
            }
        }
    }

    public ValueTask CancelCurrentAsync()
    {
        CancellationTokenSource? cancellation;
        lock (sync)
        {
            cancellation = disposed ? null : current.Cancellation;
        }

        return cancellation is null
            ? ValueTask.CompletedTask
            : new ValueTask(cancellation.CancelAsync());
    }

    public void Release(FileBrowserSourceRevision revision)
    {
        lock (sync)
        {
            if (revision.State.ActiveLeaseCount <= 0)
            {
                throw new InvalidOperationException("A source-state lease was released more than once.");
            }

            revision.State.ActiveLeaseCount--;
            TryDisposeRetired(revision.State);
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (FileBrowserSourceGeneration state in generations)
            {
                state.Cancellation.Dispose();
            }

            generations.Clear();
        }
    }

    private void TryDisposeRetired(FileBrowserSourceGeneration state)
    {
        if (!state.IsRetired
            || !state.CancellationCompleted
            || state.ActiveLeaseCount != 0
            || !generations.Remove(state))
        {
            return;
        }

        state.Cancellation.Dispose();
        disposedGenerationCount++;
    }
}

internal sealed class FileBrowserSourceGeneration(long generation)
{
    public long Generation { get; } = generation;

    public CancellationTokenSource Cancellation { get; } = new();

    public int ActiveLeaseCount { get; set; }

    public bool IsRetired { get; set; }

    public bool CancellationCompleted { get; set; }
}

internal readonly record struct FileBrowserSourceRevision(FileBrowserSourceGeneration State)
{
    public long Generation => State.Generation;

    public CancellationToken Token => State.Cancellation.Token;
}

internal readonly record struct FileBrowserSourceRevisionChange(
    long Generation,
    FileBrowserSourceGeneration Previous);
