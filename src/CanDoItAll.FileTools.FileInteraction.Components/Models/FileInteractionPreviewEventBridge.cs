namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Marshals preview runtime events back onto the owning Blazor renderer.</summary>
internal sealed class FileInteractionPreviewEventBridge
{
    private readonly Func<FileInteractionPreviewRuntime?> getCurrent;
    private readonly Func<bool> isDisposed;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly Action requestRender;
    private readonly Func<Exception, Task> dispatchException;

    public FileInteractionPreviewEventBridge(
        Func<FileInteractionPreviewRuntime?> getCurrent,
        Func<bool> isDisposed,
        Func<Func<Task>, Task> invokeAsync,
        Action requestRender,
        Func<Exception, Task> dispatchException)
    {
        this.getCurrent = getCurrent ?? throw new ArgumentNullException(nameof(getCurrent));
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        this.invokeAsync = invokeAsync ?? throw new ArgumentNullException(nameof(invokeAsync));
        this.requestRender = requestRender ?? throw new ArgumentNullException(nameof(requestRender));
        this.dispatchException = dispatchException ?? throw new ArgumentNullException(nameof(dispatchException));
    }

    public void Attach(FileInteractionPreviewRuntime preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        preview.Changed += OnChanged;
        preview.Faulted += OnFaulted;
    }

    public void Detach(FileInteractionPreviewRuntime preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        preview.Changed -= OnChanged;
        preview.Faulted -= OnFaulted;
    }

    private async void OnChanged(object? sender, EventArgs args)
    {
        try
        {
            await invokeAsync(() =>
            {
                if (!isDisposed() && ReferenceEquals(sender, getCurrent()))
                {
                    requestRender();
                }

                return Task.CompletedTask;
            });
        }
        catch (Exception exception)
        {
            await dispatchException(exception);
        }
    }

    private async void OnFaulted(object? sender, FileInteractionPreviewFaultEventArgs args)
    {
        if (ReferenceEquals(sender, getCurrent()))
        {
            await dispatchException(args.Error);
        }
    }
}
