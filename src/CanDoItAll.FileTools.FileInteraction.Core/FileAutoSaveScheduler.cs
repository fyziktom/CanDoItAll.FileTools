namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>Owns idle, interval, edit-count, text-unit-count, and composite trigger scheduling.</summary>
internal sealed class FileAutoSaveScheduler : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly FileAutoSaveOptions options;
    private readonly IFileInteractionDelay delay;
    private readonly Func<Task<FileSaveOperationResult>> requestSave;
    private readonly Func<bool> canRequestSave;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly HashSet<Task> backgroundTasks = [];
    private CancellationTokenSource? idleCancellation;
    private int changesSinceThreshold;
    private int textUnitsSinceThreshold;
    private bool savePendingWhileUnavailable;
    private bool disposed;

    public FileAutoSaveScheduler(
        FileAutoSaveOptions options,
        IFileInteractionDelay delay,
        Func<Task<FileSaveOperationResult>> requestSave,
        Func<bool>? canRequestSave = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.delay = delay ?? throw new ArgumentNullException(nameof(delay));
        this.requestSave = requestSave ?? throw new ArgumentNullException(nameof(requestSave));
        this.canRequestSave = canRequestSave ?? (() => true);

        if (options.Triggers.HasFlag(FileAutoSaveTriggers.Interval))
        {
            Track(RunIntervalAsync(lifetimeCancellation.Token));
        }
    }

    public void NotifyChanged(int changedTextUnits = 0)
    {
        if (changedTextUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedTextUnits));
        }

        var requestForThreshold = false;
        Task? idleTask = null;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (options.Triggers.HasFlag(FileAutoSaveTriggers.ChangeCount))
            {
                changesSinceThreshold++;
                if (changesSinceThreshold >= options.ChangeCount!.Value)
                {
                    changesSinceThreshold = 0;
                    requestForThreshold = true;
                }
            }

            if (options.Triggers.HasFlag(FileAutoSaveTriggers.TextUnitCount)
                && changedTextUnits > 0)
            {
                textUnitsSinceThreshold = Math.Min(
                    options.TextUnitCount!.Value,
                    (int)Math.Min(
                        int.MaxValue,
                        (long)textUnitsSinceThreshold + changedTextUnits));
                if (textUnitsSinceThreshold >= options.TextUnitCount.Value)
                {
                    textUnitsSinceThreshold = 0;
                    requestForThreshold = true;
                }
            }

            if (options.Triggers.HasFlag(FileAutoSaveTriggers.Idle))
            {
                idleCancellation?.Cancel();
                idleCancellation?.Dispose();
                idleCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
                idleTask = RequestAfterDelayAsync(options.IdleDelay!.Value, idleCancellation.Token);
            }
        }

        if (idleTask is not null)
        {
            Track(idleTask);
        }

        if (requestForThreshold)
        {
            RequestSaveIfAvailable();
        }
    }

    public void NotifyAvailabilityChanged()
    {
        var requestPendingSave = false;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (savePendingWhileUnavailable && canRequestSave())
            {
                savePendingWhileUnavailable = false;
                requestPendingSave = true;
            }
        }

        if (requestPendingSave)
        {
            Observe(requestSave());
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task[] tasks;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            idleCancellation?.Cancel();
            lifetimeCancellation.Cancel();
            tasks = backgroundTasks.ToArray();
        }

        foreach (var task in tasks)
        {
            await IgnoreCancellationAsync(task).ConfigureAwait(false);
        }

        idleCancellation?.Dispose();
        lifetimeCancellation.Dispose();
    }

    private async Task RunIntervalAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await delay.DelayAsync(options.Interval!.Value, cancellationToken).ConfigureAwait(false);
                await RequestSaveIfAvailableAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RequestAfterDelayAsync(TimeSpan dueTime, CancellationToken cancellationToken)
    {
        try
        {
            await delay.DelayAsync(dueTime, cancellationToken).ConfigureAwait(false);
            await RequestSaveIfAvailableAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void Track(Task task)
    {
        lock (gate)
        {
            backgroundTasks.RemoveWhere(existing => existing.IsCompleted);
            backgroundTasks.Add(task);
        }
    }

    private void RequestSaveIfAvailable()
    {
        var available = canRequestSave();
        lock (gate)
        {
            if (!available)
            {
                savePendingWhileUnavailable = true;
                return;
            }

            savePendingWhileUnavailable = false;
        }

        Observe(requestSave());
    }

    private Task RequestSaveIfAvailableAsync()
    {
        var available = canRequestSave();
        lock (gate)
        {
            if (!available)
            {
                savePendingWhileUnavailable = true;
                return Task.CompletedTask;
            }

            savePendingWhileUnavailable = false;
        }

        return requestSave();
    }

    private static void Observe(Task task)
    {
        _ = IgnoreCancellationAsync(task);
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
}
