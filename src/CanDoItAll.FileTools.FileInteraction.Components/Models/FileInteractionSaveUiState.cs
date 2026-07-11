namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Owns save/error presentation transitions independently of the Razor shell.</summary>
internal sealed class FileInteractionSaveUiState
{
    public FileInteractionSaveActivity Activity { get; private set; } = FileInteractionSaveActivity.Saved;

    public Exception? Error { get; private set; }

    public string ErrorMessage => Error switch
    {
        FileSaveConflictException =>
            "The file changed outside this editor. Rebase or request an overwrite before retrying.",
        _ => "The host could not persist the file. Your changes remain available."
    };

    public bool CanSave(FileInteractionEditingRuntime? editing, bool hostCanSave)
        => editing is not null
            && editing.CanSave
            && hostCanSave
            && Activity != FileInteractionSaveActivity.Saved
            && !editing.State.IsSaving
            && !editing.State.HasConflict
            && editing.State.IsDirty;

    public string Status(FileInteractionEditingRuntime? editing, bool hostCanSave, FileInteractionMode mode)
    {
        if (mode != FileInteractionMode.Edit)
        {
            return "View mode";
        }

        if (editing is null)
        {
            return "Persistence unavailable";
        }

        return Activity switch
        {
            FileInteractionSaveActivity.Saving => "Saving…",
            FileInteractionSaveActivity.Failed => "Save failed",
            FileInteractionSaveActivity.Conflict => "Save conflict",
            _ when !hostCanSave || !editing.CanSave => editing.State.IsDirty
                ? "Read-only persistence"
                : "Persistence unavailable",
            FileInteractionSaveActivity.Dirty => "Unsaved changes",
            _ => "Saved"
        };
    }

    public void Reset()
    {
        Activity = FileInteractionSaveActivity.Saved;
        Error = null;
    }

    public void MarkDirty()
    {
        if (Activity != FileInteractionSaveActivity.Saving)
        {
            Activity = FileInteractionSaveActivity.Dirty;
        }

        Error = null;
    }

    public void MarkSaving()
    {
        Activity = FileInteractionSaveActivity.Saving;
        Error = null;
    }

    public void Synchronize(FileEditSessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Error = state.LastSaveError;
        Activity = state switch
        {
            { HasConflict: true } => FileInteractionSaveActivity.Conflict,
            { LastSaveError: not null } => FileInteractionSaveActivity.Failed,
            { IsSaving: true } => FileInteractionSaveActivity.Saving,
            { IsDirty: true } => FileInteractionSaveActivity.Dirty,
            _ => FileInteractionSaveActivity.Saved
        };
    }

}
