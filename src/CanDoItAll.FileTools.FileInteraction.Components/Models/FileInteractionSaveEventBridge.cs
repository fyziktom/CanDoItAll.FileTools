namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>
/// Marshals save lifecycle notifications onto the owning Blazor renderer and rejects notifications
/// from superseded editing runtimes.
/// </summary>
internal sealed class FileInteractionSaveEventBridge
{
    private readonly FileInteractionSaveUiState saveUi;
    private readonly Func<FileInteractionEditingRuntime?> getCurrent;
    private readonly Func<int, FileInteractionEditingRuntime, bool> isCurrent;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly Action requestRender;
    private readonly Func<ValueTask> publishState;
    private readonly Func<Exception, Task> dispatchException;
    private readonly object attachmentGate = new();
    private FileInteractionEditingRuntime? attached;
    private int attachedGeneration;
    private long latestCompletion;

    public FileInteractionSaveEventBridge(
        FileInteractionSaveUiState saveUi,
        Func<FileInteractionEditingRuntime?> getCurrent,
        Func<int, FileInteractionEditingRuntime, bool> isCurrent,
        Func<Func<Task>, Task> invokeAsync,
        Action requestRender,
        Func<ValueTask> publishState,
        Func<Exception, Task> dispatchException)
    {
        this.saveUi = saveUi ?? throw new ArgumentNullException(nameof(saveUi));
        this.getCurrent = getCurrent ?? throw new ArgumentNullException(nameof(getCurrent));
        this.isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        this.invokeAsync = invokeAsync ?? throw new ArgumentNullException(nameof(invokeAsync));
        this.requestRender = requestRender ?? throw new ArgumentNullException(nameof(requestRender));
        this.publishState = publishState ?? throw new ArgumentNullException(nameof(publishState));
        this.dispatchException = dispatchException ?? throw new ArgumentNullException(nameof(dispatchException));
    }

    public void Attach(FileInteractionEditingRuntime runtime, int operationGeneration)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Detach();
        lock (attachmentGate)
        {
            attached = runtime;
            attachedGeneration = operationGeneration;
            latestCompletion = 0;
            runtime.SaveCompleted += OnSaveCompleted;
        }
    }

    public void Detach()
    {
        lock (attachmentGate)
        {
            if (attached is not null)
            {
                attached.SaveCompleted -= OnSaveCompleted;
                attached = null;
            }
        }
    }

    public Task OnHostSaveStartingAsync(FileSaveRequest request, int operationGeneration)
    {
        ArgumentNullException.ThrowIfNull(request);
        var runtime = getCurrent();
        return runtime is null || !isCurrent(operationGeneration, runtime)
            ? Task.CompletedTask
            : invokeAsync(async () =>
            {
                if (isCurrent(operationGeneration, runtime)
                    && runtime.State.Current.File == request.File)
                {
                    saveUi.MarkSaving();
                    requestRender();
                    await publishState();
                }
            });
    }

    private void OnSaveCompleted(object? sender, FileSaveCompletedEventArgs eventArgs)
    {
        FileInteractionEditingRuntime runtime;
        int operationGeneration;
        long completion;
        lock (attachmentGate)
        {
            if (attached is null || !ReferenceEquals(sender, attached))
            {
                return;
            }

            runtime = attached;
            operationGeneration = attachedGeneration;
            completion = ++latestCompletion;
        }

        _ = ObserveCompletionAsync(runtime, operationGeneration, completion);
    }

    private async Task ObserveCompletionAsync(
        FileInteractionEditingRuntime runtime,
        int operationGeneration,
        long completion)
    {
        try
        {
            await invokeAsync(async () =>
            {
                if (!isCurrent(operationGeneration, runtime))
                {
                    return;
                }

                if (completion != Interlocked.Read(ref latestCompletion))
                {
                    return;
                }

                // A queued older completion converges to the runtime's current truth instead of
                // reapplying a stale result after a newer save has started or finished.
                saveUi.Synchronize(runtime.State);
                await publishState();
                if (isCurrent(operationGeneration, runtime)
                    && completion == Interlocked.Read(ref latestCompletion))
                {
                    requestRender();
                }
            });
        }
        catch (Exception exception)
        {
            await dispatchException(exception);
        }
    }
}
