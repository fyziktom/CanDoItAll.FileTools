namespace CanDoItAll.FileTools.FileInteraction.Core.Tests;

internal sealed class ManualFileInteractionDelay : IFileInteractionDelay
{
    private readonly object gate = new();
    private readonly List<PendingDelay> pending = [];

    public int ActiveCount
    {
        get
        {
            lock (gate)
            {
                return pending.Count(value => !value.Completion.Task.IsCompleted);
            }
        }
    }

    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        lock (gate)
        {
            pending.Add(new PendingDelay(delay, completion));
        }

        return new ValueTask(completion.Task);
    }

    public void ReleaseNext()
    {
        PendingDelay? value;
        lock (gate)
        {
            value = pending.FirstOrDefault(candidate => !candidate.Completion.Task.IsCompleted);
        }

        Assert.NotNull(value);
        value.Completion.TrySetResult();
    }

    private sealed record PendingDelay(TimeSpan Delay, TaskCompletionSource Completion);
}

internal static class TestWait
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(1);

    public static async Task UntilAsync(
        Func<bool> condition,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var effectiveTimeout = timeout ?? DefaultTimeout;
        if (effectiveTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var startedAt = TimeProvider.System.GetTimestamp();
        while (!condition())
        {
            if (TimeProvider.System.GetElapsedTime(startedAt) >= effectiveTimeout)
            {
                Assert.Fail(
                    $"The expected asynchronous condition did not become true within {effectiveTimeout}.");
            }

            await Task.Delay(PollInterval);
        }
    }

    public static async Task YieldSeveralAsync()
    {
        for (var index = 0; index < 10; index++)
        {
            await Task.Yield();
        }
    }
}

internal sealed class ControlledSaveTarget : IFileSaveTarget
{
    private readonly object gate = new();
    private readonly List<PendingSave> pending = [];
    private int concurrency;

    public IReadOnlyList<FileSaveRequest> Requests
    {
        get
        {
            lock (gate)
            {
                return pending.Select(value => value.Request).ToArray();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return pending.Count;
            }
        }
    }

    public int MaximumConcurrency { get; private set; }

    public ValueTask<FileSaveTargetResult> SaveAsync(FileSaveRequest request, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<FileSaveTargetResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            concurrency++;
            MaximumConcurrency = Math.Max(MaximumConcurrency, concurrency);
            pending.Add(new PendingSave(request, completion));
        }

        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return new ValueTask<FileSaveTargetResult>(CompleteAsync(completion.Task));
    }

    public void Succeed(int index, string revision)
        => pending[index].Completion.TrySetResult(new FileSaveTargetResult(new FileContentRevision(revision)));

    public void SucceedWithoutRevision(int index)
        => pending[index].Completion.TrySetResult(new FileSaveTargetResult());

    public void Fail(int index, Exception error)
        => pending[index].Completion.TrySetException(error);

    private async Task<FileSaveTargetResult> CompleteAsync(Task<FileSaveTargetResult> task)
    {
        try
        {
            return await task;
        }
        finally
        {
            lock (gate)
            {
                concurrency--;
            }
        }
    }

    private sealed record PendingSave(
        FileSaveRequest Request,
        TaskCompletionSource<FileSaveTargetResult> Completion);
}
