namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>An immutable view of one edit session's persisted and in-memory revisions.</summary>
public sealed record FileEditSessionState(
    FileEditSnapshot Current,
    FileContentRevision? BaseRevision,
    long SavedEditRevision,
    long? SavingEditRevision,
    Exception? LastSaveError,
    bool HasConflict)
{
    public long EditRevision => Current.EditRevision;

    public bool IsDirty => EditRevision != SavedEditRevision;

    public bool IsSaving => SavingEditRevision.HasValue;
}

/// <summary>
/// Owns file/edit/base revision transitions. Persistence is performed by a separate save target.
/// </summary>
public sealed class FileEditSession
{
    private readonly object gate = new();
    private FileEditSnapshot current;
    private FileContentRevision? baseRevision;
    private long savedEditRevision;
    private long? savingEditRevision;
    private Exception? lastSaveError;
    private bool hasConflict;

    public FileEditSession(FileEditSnapshot initialSnapshot, FileContentRevision? baseRevision = null)
    {
        current = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
        this.baseRevision = baseRevision;
        savedEditRevision = initialSnapshot.EditRevision;
    }

    public FileEditSessionState State
    {
        get
        {
            lock (gate)
            {
                return SnapshotState();
            }
        }
    }

    public FileEditSnapshot ApplyEdit(
        ReadOnlyMemory<byte> content,
        string? mediaType = null,
        string? encodingName = null)
    {
        lock (gate)
        {
            current = new FileEditSnapshot(
                current.File,
                checked(current.EditRevision + 1),
                content,
                mediaType ?? current.MediaType,
                encodingName ?? current.EncodingName);
            if (!hasConflict)
            {
                lastSaveError = null;
            }

            return current;
        }
    }

    internal FileSaveRequest? TryBeginSave(bool isAutomatic)
    {
        lock (gate)
        {
            if (current.EditRevision == savedEditRevision || hasConflict)
            {
                return null;
            }

            if (savingEditRevision.HasValue)
            {
                throw new InvalidOperationException("Only one save may be active for an edit session.");
            }

            savingEditRevision = current.EditRevision;
            lastSaveError = null;
            hasConflict = false;
            return new FileSaveRequest(
                current.File,
                current.EditRevision,
                new BufferedFileSaveContent(current.Content),
                baseRevision,
                current.MediaType,
                current.EncodingName,
                isAutomatic);
        }
    }

    internal bool AcknowledgeSave(
        long editRevision,
        FileContentRevision? persistedRevision)
    {
        lock (gate)
        {
            if (savingEditRevision != editRevision)
            {
                return false;
            }

            savingEditRevision = null;
            savedEditRevision = Math.Max(savedEditRevision, editRevision);
            baseRevision = persistedRevision;

            lastSaveError = null;
            hasConflict = false;
            return true;
        }
    }

    internal bool RejectSave(long editRevision, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        lock (gate)
        {
            if (savingEditRevision != editRevision)
            {
                return false;
            }

            savingEditRevision = null;
            lastSaveError = error;
            hasConflict = error is FileSaveConflictException;
            return true;
        }
    }

    internal bool CancelSave(long editRevision)
    {
        lock (gate)
        {
            if (savingEditRevision != editRevision)
            {
                return false;
            }

            savingEditRevision = null;
            return true;
        }
    }

    internal FileEditSessionState ResolveConflict(FileContentRevision? expectedRevision)
    {
        lock (gate)
        {
            if (!hasConflict)
            {
                throw new InvalidOperationException("The edit session has no save conflict to resolve.");
            }

            if (savingEditRevision.HasValue)
            {
                throw new InvalidOperationException("A save conflict cannot be resolved while persistence is active.");
            }

            baseRevision = expectedRevision;
            lastSaveError = null;
            hasConflict = false;
            return SnapshotState();
        }
    }

    private FileEditSessionState SnapshotState()
        => new(current, baseRevision, savedEditRevision, savingEditRevision, lastSaveError, hasConflict);
}

/// <summary>A replayable, defensively copied save payload backed by memory.</summary>
public sealed class BufferedFileSaveContent : IFileSaveContent
{
    private readonly byte[] content;

    public BufferedFileSaveContent(ReadOnlyMemory<byte> content)
    {
        this.content = content.ToArray();
    }

    public long? Length => content.LongLength;

    public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false));
    }
}
