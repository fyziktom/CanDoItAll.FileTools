using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components;

internal sealed class EventCallbackFileSaveTarget : IFileSaveTarget
{
    private readonly Func<EventCallback<FileInteractionSaveRequestedEventArgs>> callback;
    private readonly Func<FileSaveRequest, Task> onStarting;

    public EventCallbackFileSaveTarget(
        Func<EventCallback<FileInteractionSaveRequestedEventArgs>> callback,
        Func<FileSaveRequest, Task> onStarting)
    {
        this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        this.onStarting = onStarting ?? throw new ArgumentNullException(nameof(onStarting));
    }

    public async ValueTask<FileSaveTargetResult> SaveAsync(
        FileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var hostCallback = callback();
        if (!hostCallback.HasDelegate)
        {
            throw new InvalidOperationException("The host did not provide a save callback.");
        }

        await onStarting(request).ConfigureAwait(false);
        var args = new FileInteractionSaveRequestedEventArgs(request);
        await hostCallback.InvokeAsync(args).ConfigureAwait(false);
        return new FileSaveTargetResult(
            args.HasPersistedRevision ? args.PersistedRevision : null);
    }
}

internal sealed class IdentityFilePreviewGenerator : IFilePreviewGenerator<ReadOnlyMemory<byte>>
{
    public ValueTask<ReadOnlyMemory<byte>> GenerateAsync(
        FileEditSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(snapshot.Content);
    }
}
