namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>Generates one UI-neutral preview value for an immutable edit snapshot.</summary>
public interface IFilePreviewGenerator<TPreview>
{
    ValueTask<TPreview> GenerateAsync(
        FileEditSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

public sealed record FilePreviewUpdate<TPreview>(
    FileReference File,
    long EditRevision,
    TPreview Preview,
    string? MediaType = null,
    string? EncodingName = null);

/// <summary>Debounces preview work and prevents stale completions from replacing newer revisions.</summary>
public sealed class FilePreviewCoordinator<TPreview> : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly IFilePreviewGenerator<TPreview> generator;
    private readonly IFileInteractionDelay delay;
    private readonly TimeSpan debounce;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly HashSet<FilePreviewRequest<TPreview>> activeRequests = [];
    private FilePreviewRequest<TPreview>? currentRequest;
    private long generation;
    private bool disposed;
    private FilePreviewUpdate<TPreview>? current;

    public FilePreviewCoordinator(
        IFilePreviewGenerator<TPreview> generator,
        TimeSpan debounce,
        IFileInteractionDelay? delay = null)
    {
        this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
        if (debounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce));
        }

        this.debounce = debounce;
        this.delay = delay ?? SystemFileInteractionDelay.Instance;
    }

    public FilePreviewUpdate<TPreview>? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public ValueTask<FilePreviewUpdate<TPreview>?> RequestAsync(FileEditSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        FilePreviewRequest<TPreview>? previousRequest;
        FilePreviewRequest<TPreview> request;
        long requestGeneration;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            previousRequest = currentRequest;
            request = new FilePreviewRequest<TPreview>(lifetimeCancellation.Token);
            currentRequest = request;
            activeRequests.Add(request);
            requestGeneration = ++generation;
        }

        _ = ExecuteRequestAsync(request, snapshot, requestGeneration);
        previousRequest?.Cancel();
        return new ValueTask<FilePreviewUpdate<TPreview>?>(request.Completion);
    }

    public async ValueTask DisposeAsync()
    {
        FilePreviewRequest<TPreview>[] requests;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            generation++;
            currentRequest = null;
            requests = activeRequests.ToArray();
        }

        lifetimeCancellation.Cancel();
        foreach (var request in requests)
        {
            await ObserveCompletionAsync(request.Completion).ConfigureAwait(false);
        }

        lifetimeCancellation.Dispose();
    }

    private async Task ExecuteRequestAsync(
        FilePreviewRequest<TPreview> request,
        FileEditSnapshot snapshot,
        long requestGeneration)
    {
        FilePreviewUpdate<TPreview>? result = null;
        Exception? error = null;
        try
        {
            result = await ExecuteAsync(snapshot, requestGeneration, request.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = exception;
        }
        finally
        {
            lock (gate)
            {
                activeRequests.Remove(request);
                if (ReferenceEquals(currentRequest, request))
                {
                    currentRequest = null;
                }
            }

            request.Dispose();
        }

        if (error is null)
        {
            request.TrySetResult(result);
        }
        else
        {
            request.TrySetException(error);
        }
    }

    private async Task<FilePreviewUpdate<TPreview>?> ExecuteAsync(
        FileEditSnapshot snapshot,
        long requestGeneration,
        CancellationToken cancellationToken)
    {
        try
        {
            await delay.DelayAsync(debounce, cancellationToken).ConfigureAwait(false);
            var preview = await generator.GenerateAsync(snapshot, cancellationToken).ConfigureAwait(false);
            var update = new FilePreviewUpdate<TPreview>(
                snapshot.File,
                snapshot.EditRevision,
                preview,
                snapshot.MediaType,
                snapshot.EncodingName);
            lock (gate)
            {
                if (disposed || requestGeneration != generation)
                {
                    return null;
                }

                current = update;
                return update;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task ObserveCompletionAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // The request task still preserves its exception for its caller; disposal only drains owned work.
        }
    }
}

internal sealed class FilePreviewRequest<TPreview> : IDisposable
{
    private readonly CancellationTokenSource cancellation;
    private readonly TaskCompletionSource<FilePreviewUpdate<TPreview>?> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FilePreviewRequest(CancellationToken lifetimeToken)
    {
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
    }

    public CancellationToken Token => cancellation.Token;

    public Task<FilePreviewUpdate<TPreview>?> Completion => completion.Task;

    public void Cancel() => cancellation.Cancel();

    public void TrySetResult(FilePreviewUpdate<TPreview>? result) => completion.TrySetResult(result);

    public void TrySetException(Exception error) => completion.TrySetException(error);

    public void Dispose() => cancellation.Dispose();
}
