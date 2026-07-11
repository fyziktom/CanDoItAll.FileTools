using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Owns the host-authoritative mode transition and stale-operation ordering contract.</summary>
internal sealed class FileInteractionModeController
{
    private readonly FileInteractionSurfaceBinding surface;
    private readonly Func<bool> isDisposed;
    private readonly Func<int> getGeneration;
    private readonly Func<EventCallback<FileInteractionMode>> getModeChanged;
    private readonly Func<int, bool> isCurrent;
    private readonly Func<ValueTask> publishState;
    private readonly Func<Task> ensureEditing;

    public FileInteractionModeController(
        FileInteractionSurfaceBinding surface,
        Func<bool> isDisposed,
        Func<int> getGeneration,
        Func<EventCallback<FileInteractionMode>> getModeChanged,
        Func<int, bool> isCurrent,
        Func<ValueTask> publishState,
        Func<Task> ensureEditing)
    {
        this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        this.getGeneration = getGeneration ?? throw new ArgumentNullException(nameof(getGeneration));
        this.getModeChanged = getModeChanged ?? throw new ArgumentNullException(nameof(getModeChanged));
        this.isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        this.publishState = publishState ?? throw new ArgumentNullException(nameof(publishState));
        this.ensureEditing = ensureEditing ?? throw new ArgumentNullException(nameof(ensureEditing));
    }

    public async Task ChangeAsync(FileInteractionMode mode, bool notifyHost)
    {
        if (isDisposed()
            || surface.State != FileInteractionLoadState.Loaded
            || surface.Request is null)
        {
            return;
        }

        var operationGeneration = getGeneration();
        var modeChanged = getModeChanged();
        if (!surface.TrySetMode(mode))
        {
            return;
        }

        if (notifyHost && modeChanged.HasDelegate)
        {
            await modeChanged.InvokeAsync(mode);
            await Task.Yield();
            if (!IsCurrentMode(operationGeneration, mode))
            {
                return;
            }
        }

        await publishState();
        await Task.Yield();
        if (!IsCurrentMode(operationGeneration, mode))
        {
            return;
        }

        if (mode == FileInteractionMode.Edit)
        {
            await ensureEditing();
        }

        if (IsCurrentMode(operationGeneration, mode))
        {
            await publishState();
        }
    }

    private bool IsCurrentMode(int operationGeneration, FileInteractionMode mode)
        => isCurrent(operationGeneration) && surface.Mode == mode;
}
