namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Owns cancellable construction and replacement of one editing runtime.</summary>
internal sealed class FileInteractionEditingBinding : IAsyncDisposable
{
    private CancellationTokenSource? creationCancellation;
    private int version;

    public FileInteractionEditingRuntime? Current { get; private set; }

    public async ValueTask<FileInteractionEditingRuntime?> ReplaceAsync(
        FileInteractionProfileDescriptor profile,
        FileInteractionContentKind contentKind,
        FileInteractionRequest request,
        FileInteractionComponentComposition composition,
        ReadOnlyMemory<byte> content,
        int maximumContentBytes,
        IFileSaveTarget saveTarget,
        Func<bool> canAutoSave,
        Func<bool> isSurfaceCurrent)
    {
        ArgumentNullException.ThrowIfNull(isSurfaceCurrent);
        await ResetAsync().ConfigureAwait(false);
        if (!isSurfaceCurrent())
        {
            return null;
        }

        var cancellation = new CancellationTokenSource();
        var creationVersion = ++version;
        creationCancellation = cancellation;
        FileInteractionEditingRuntime created;
        try
        {
            created = await FileInteractionEditingRuntime.CreateAsync(
                profile,
                contentKind,
                request,
                composition,
                content,
                maximumContentBytes,
                saveTarget,
                canAutoSave,
                cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            (!isSurfaceCurrent() || creationVersion != version)
            && exception is OperationCanceledException or ObjectDisposedException)
        {
            return null;
        }
        finally
        {
            if (ReferenceEquals(creationCancellation, cancellation))
            {
                creationCancellation = null;
            }

            cancellation.Dispose();
        }

        if (!isSurfaceCurrent() || creationVersion != version)
        {
            await created.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        Current = created;
        return created;
    }

    public async ValueTask ResetAsync()
    {
        version++;
        creationCancellation?.Cancel();
        creationCancellation = null;
        var previous = Current;
        Current = null;
        if (previous is not null)
        {
            await previous.DisposeAsync().ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync() => ResetAsync();
}

internal readonly record struct FileInteractionOperationResult<T>(bool IsCurrent, T? Value);

/// <summary>Centralizes supersession handling for awaited editor/history/save operations.</summary>
internal static class FileInteractionOperationExecutor
{
    public static async ValueTask<FileInteractionOperationResult<T>> ExecuteAsync<T>(
        Func<ValueTask<T>> operation,
        Func<bool> isCurrent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(isCurrent);
        try
        {
            var result = await operation().ConfigureAwait(false);
            return isCurrent()
                ? new FileInteractionOperationResult<T>(true, result)
                : default;
        }
        catch (Exception exception) when (
            !isCurrent() && exception is OperationCanceledException or ObjectDisposedException)
        {
            return default;
        }
    }
}
