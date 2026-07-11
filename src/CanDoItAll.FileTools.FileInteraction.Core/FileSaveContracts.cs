namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>Persists a host-owned save request and returns the new persisted revision.</summary>
public interface IFileSaveTarget
{
    ValueTask<FileSaveTargetResult> SaveAsync(
        FileSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record FileSaveTargetResult(FileContentRevision? PersistedRevision = null);

public enum FileSaveOperationStatus
{
    NotDirty,
    Saved,
    Failed,
    Conflict,
    Cancelled
}

public sealed record FileSaveOperationResult(
    FileSaveOperationStatus Status,
    long? EditRevision = null,
    FileContentRevision? PersistedRevision = null,
    Exception? Error = null);

/// <summary>
/// Describes a completed persistence attempt after its result has been applied to the edit session.
/// </summary>
public sealed class FileSaveCompletedEventArgs : EventArgs
{
    public FileSaveCompletedEventArgs(
        FileSaveOperationResult result,
        FileEditSessionState state)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public FileSaveOperationResult Result { get; }

    /// <summary>The immutable session state captured after the persistence transition.</summary>
    public FileEditSessionState State { get; }
}
