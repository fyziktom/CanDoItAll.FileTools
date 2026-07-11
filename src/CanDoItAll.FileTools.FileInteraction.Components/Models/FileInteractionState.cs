using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components;

public enum FileInteractionLifecycleState
{
    Loading,
    Loaded,
    Unsupported,
    Error
}

/// <summary>Immutable host-facing state used for close guards and surrounding window chrome.</summary>
public sealed record FileInteractionState(
    FileReference? File,
    string? FileName,
    FileInteractionMode Mode,
    FileInteractionLifecycleState Lifecycle,
    long EditRevision,
    bool IsDirty,
    bool IsSaving,
    bool HasConflict,
    bool CanUndo,
    bool CanRedo,
    bool HasError);

internal static class FileInteractionStateFactory
{
    public static FileInteractionState Create(
        FileInteractionSurfaceBinding surface,
        FileInteractionEditingRuntime? editing,
        FileInteractionSaveUiState save,
        FileInteractionEditUiState edit)
    {
        var editState = editing?.State;
        return new FileInteractionState(
            surface.Request?.File,
            surface.Request?.FileName,
            surface.Mode,
            surface.State switch
            {
                FileInteractionLoadState.Loaded => FileInteractionLifecycleState.Loaded,
                FileInteractionLoadState.Unsupported => FileInteractionLifecycleState.Unsupported,
                FileInteractionLoadState.Error => FileInteractionLifecycleState.Error,
                _ => FileInteractionLifecycleState.Loading
            },
            editState?.EditRevision ?? 0,
            editState?.IsDirty == true,
            editState?.IsSaving == true || save.Activity == FileInteractionSaveActivity.Saving,
            save.Activity == FileInteractionSaveActivity.Conflict || editState?.HasConflict == true,
            editing?.CanUndo == true,
            editing?.CanRedo == true,
            surface.State == FileInteractionLoadState.Error
                || save.Error is not null
                || edit.Error is not null);
    }
}

internal sealed class FileInteractionStatePublisher
{
    private EventCallback<FileInteractionState> callback;
    private FileInteractionState? last;

    public void SetCallback(EventCallback<FileInteractionState> value)
    {
        if (!callback.Equals(value))
        {
            callback = value;
            last = null;
        }
    }

    public async ValueTask PublishAsync(FileInteractionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state == last)
        {
            return;
        }

        last = state;
        if (callback.HasDelegate)
        {
            await callback.InvokeAsync(state);
        }
    }
}
