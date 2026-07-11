namespace CanDoItAll.FileTools.FileInteraction;

[Flags]
public enum FileAutoSaveTriggers
{
    None = 0,
    Interval = 1 << 0,
    Idle = 1 << 1,
    ChangeCount = 1 << 2,
    /// <summary>Saves after cumulative changed UTF-16 text units reach the configured threshold.</summary>
    TextUnitCount = 1 << 3
}

/// <summary>Validated automatic-save strategy. Manual save is independent of these triggers.</summary>
public sealed record FileAutoSaveOptions
{
    public FileAutoSaveOptions(
        FileAutoSaveTriggers triggers = FileAutoSaveTriggers.None,
        TimeSpan? interval = null,
        TimeSpan? idleDelay = null,
        int? changeCount = null,
        int? textUnitCount = null)
    {
        const FileAutoSaveTriggers supportedTriggers = FileAutoSaveTriggers.Interval
            | FileAutoSaveTriggers.Idle
            | FileAutoSaveTriggers.ChangeCount
            | FileAutoSaveTriggers.TextUnitCount;
        if ((triggers & ~supportedTriggers) != FileAutoSaveTriggers.None)
        {
            throw new ArgumentOutOfRangeException(nameof(triggers), triggers, "Unsupported automatic-save trigger flags were provided.");
        }

        if (triggers.HasFlag(FileAutoSaveTriggers.Interval)
            && (interval is null || interval <= TimeSpan.Zero))
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        if (triggers.HasFlag(FileAutoSaveTriggers.Idle)
            && (idleDelay is null || idleDelay <= TimeSpan.Zero))
        {
            throw new ArgumentOutOfRangeException(nameof(idleDelay));
        }

        if (triggers.HasFlag(FileAutoSaveTriggers.ChangeCount)
            && (changeCount is null || changeCount <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(changeCount));
        }

        if (triggers.HasFlag(FileAutoSaveTriggers.TextUnitCount)
            && (textUnitCount is null || textUnitCount <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(textUnitCount));
        }

        if (!triggers.HasFlag(FileAutoSaveTriggers.Interval) && interval is not null
            || !triggers.HasFlag(FileAutoSaveTriggers.Idle) && idleDelay is not null
            || !triggers.HasFlag(FileAutoSaveTriggers.ChangeCount) && changeCount is not null
            || !triggers.HasFlag(FileAutoSaveTriggers.TextUnitCount) && textUnitCount is not null)
        {
            throw new ArgumentException("A trigger value was provided without enabling its trigger.");
        }

        Triggers = triggers;
        Interval = interval;
        IdleDelay = idleDelay;
        ChangeCount = changeCount;
        TextUnitCount = textUnitCount;
    }

    public FileAutoSaveTriggers Triggers { get; }

    public TimeSpan? Interval { get; }

    public TimeSpan? IdleDelay { get; }

    public int? ChangeCount { get; }

    /// <summary>
    /// Cumulative changed UTF-16 code units required to request an automatic save. The UI adapter
    /// computes each replacement by removing the unchanged prefix and suffix.
    /// </summary>
    public int? TextUnitCount { get; }

    public bool Enabled => Triggers != FileAutoSaveTriggers.None;

    public static FileAutoSaveOptions Disabled { get; } = new();
}

public sealed record FileHistoryOptions
{
    public FileHistoryOptions(int maxEntries = 0, long maxBytes = 0)
    {
        if (maxEntries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries));
        }

        if (maxBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        if ((maxEntries == 0) != (maxBytes == 0))
        {
            throw new ArgumentException("History entry and byte limits must both be enabled or disabled.");
        }

        MaxEntries = maxEntries;
        MaxBytes = maxBytes;
    }

    public int MaxEntries { get; }

    public long MaxBytes { get; }

    public bool Enabled => MaxEntries > 0;

    public static FileHistoryOptions Disabled { get; } = new();
}

/// <summary>Immutable editor content passed to history and save coordination.</summary>
public sealed record FileEditSnapshot
{
    public FileEditSnapshot(
        FileReference file,
        long editRevision,
        ReadOnlyMemory<byte> content,
        string? mediaType = null,
        string? encodingName = null)
    {
        if (string.IsNullOrWhiteSpace(file.SourceId) || string.IsNullOrWhiteSpace(file.Value))
        {
            throw new ArgumentException("A valid file reference is required.", nameof(file));
        }

        if (editRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(editRevision));
        }

        File = file;
        EditRevision = editRevision;
        Content = content.ToArray();
        MediaType = FileInteractionMediaType.NormalizeOptional(mediaType);
        EncodingName = string.IsNullOrWhiteSpace(encodingName) ? null : encodingName.Trim();
    }

    public FileReference File { get; }

    public long EditRevision { get; }

    public ReadOnlyMemory<byte> Content { get; }

    public string? MediaType { get; }

    public string? EncodingName { get; }
}

public readonly record struct FileEditHistoryState
{
    public FileEditHistoryState(bool canUndo, bool canRedo, int undoDepth, int redoDepth)
    {
        if (undoDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(undoDepth));
        }

        if (redoDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(redoDepth));
        }

        if (canUndo != (undoDepth > 0))
        {
            throw new ArgumentException("CanUndo must agree with UndoDepth.", nameof(canUndo));
        }

        if (canRedo != (redoDepth > 0))
        {
            throw new ArgumentException("CanRedo must agree with RedoDepth.", nameof(canRedo));
        }

        CanUndo = canUndo;
        CanRedo = canRedo;
        UndoDepth = undoDepth;
        RedoDepth = redoDepth;
    }

    public bool CanUndo { get; }

    public bool CanRedo { get; }

    public int UndoDepth { get; }

    public int RedoDepth { get; }
}

/// <summary>A file-type-specific history provider. Absence means undo/redo is unavailable.</summary>
public interface IFileEditHistoryProvider : IAsyncDisposable
{
    FileEditHistoryState State { get; }

    ValueTask ResetAsync(
        FileReference file,
        FileContentRevision? baseRevision,
        FileEditSnapshot initialSnapshot,
        CancellationToken cancellationToken = default);

    ValueTask RecordAsync(FileEditSnapshot snapshot, CancellationToken cancellationToken = default);

    ValueTask<FileEditSnapshot?> UndoAsync(CancellationToken cancellationToken = default);

    ValueTask<FileEditSnapshot?> RedoAsync(CancellationToken cancellationToken = default);
}

public interface IFileEditHistoryProviderFactory
{
    /// <summary>
    /// Selection priority among factories whose <see cref="CanCreate"/> method returns true.
    /// Equal highest priorities are reported as ambiguous instead of depending on registration order.
    /// </summary>
    int Priority => 0;

    bool CanCreate(FileInteractionProfileDescriptor profile, FileInteractionRequest request);

    ValueTask<IFileEditHistoryProvider?> CreateAsync(
        FileInteractionProfileDescriptor profile,
        FileInteractionRequest request,
        CancellationToken cancellationToken = default);
}
