namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>
/// Stores bounded immutable text snapshots with branching undo/redo semantics.
/// The current snapshot is retained outside the configured history budget.
/// </summary>
public sealed class BoundedTextHistoryProvider : IFileEditHistoryProvider
{
    private readonly object gate = new();
    private readonly FileHistoryOptions options;
    private readonly List<FileEditSnapshot> undo = [];
    private readonly List<FileEditSnapshot> redo = [];
    private FileReference? file;
    private FileContentRevision? baseRevision;
    private FileEditSnapshot? current;
    private long latestRecordedRevision;
    private bool disposed;

    public BoundedTextHistoryProvider(FileHistoryOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        if (!options.Enabled)
        {
            throw new ArgumentException("Bounded history requires enabled entry and byte limits.", nameof(options));
        }
    }

    public FileEditHistoryState State
    {
        get
        {
            lock (gate)
            {
                return new FileEditHistoryState(undo.Count > 0, redo.Count > 0, undo.Count, redo.Count);
            }
        }
    }

    public FileReference? File
    {
        get
        {
            lock (gate)
            {
                return file;
            }
        }
    }

    public FileContentRevision? BaseRevision
    {
        get
        {
            lock (gate)
            {
                return baseRevision;
            }
        }
    }

    public ValueTask ResetAsync(
        FileReference file,
        FileContentRevision? baseRevision,
        FileEditSnapshot initialSnapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(initialSnapshot);
        if (initialSnapshot.File != file)
        {
            throw new ArgumentException("The initial history snapshot must belong to the reset file.", nameof(initialSnapshot));
        }

        lock (gate)
        {
            ThrowIfDisposed();
            this.file = file;
            this.baseRevision = baseRevision;
            current = initialSnapshot;
            latestRecordedRevision = initialSnapshot.EditRevision;
            undo.Clear();
            redo.Clear();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask RecordAsync(
        FileEditSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (gate)
        {
            ThrowIfDisposed();
            EnsureInitialized();
            if (snapshot.File != file)
            {
                throw new InvalidOperationException("History cannot record content from another file.");
            }

            if (snapshot.EditRevision <= latestRecordedRevision)
            {
                throw new InvalidOperationException("History snapshots must have a strictly increasing edit revision.");
            }

            undo.Add(current!);
            current = snapshot;
            latestRecordedRevision = snapshot.EditRevision;
            redo.Clear();
            TrimToBudget();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<FileEditSnapshot?> UndoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ThrowIfDisposed();
            EnsureInitialized();
            if (undo.Count == 0)
            {
                return ValueTask.FromResult<FileEditSnapshot?>(null);
            }

            var index = undo.Count - 1;
            var target = undo[index];
            undo.RemoveAt(index);
            redo.Add(current!);
            current = target;
            TrimToBudget();
            return ValueTask.FromResult<FileEditSnapshot?>(target);
        }
    }

    public ValueTask<FileEditSnapshot?> RedoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ThrowIfDisposed();
            EnsureInitialized();
            if (redo.Count == 0)
            {
                return ValueTask.FromResult<FileEditSnapshot?>(null);
            }

            var index = redo.Count - 1;
            var target = redo[index];
            redo.RemoveAt(index);
            undo.Add(current!);
            current = target;
            TrimToBudget();
            return ValueTask.FromResult<FileEditSnapshot?>(target);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (!disposed)
            {
                disposed = true;
                undo.Clear();
                redo.Clear();
                current = null;
                file = null;
                baseRevision = null;
            }
        }

        return ValueTask.CompletedTask;
    }

    private void TrimToBudget()
    {
        while (undo.Count + redo.Count > options.MaxEntries
            || StoredBytes() > options.MaxBytes)
        {
            if (undo.Count > 0)
            {
                undo.RemoveAt(0);
            }
            else if (redo.Count > 0)
            {
                redo.RemoveAt(0);
            }
            else
            {
                break;
            }
        }
    }

    private long StoredBytes()
        => undo.Sum(snapshot => (long)snapshot.Content.Length)
            + redo.Sum(snapshot => (long)snapshot.Content.Length);

    private void EnsureInitialized()
    {
        if (current is null || !file.HasValue)
        {
            throw new InvalidOperationException("History must be reset before it is used.");
        }
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(disposed, this);
}

/// <summary>Creates bounded history when a profile explicitly enables history and undo/redo.</summary>
public sealed class BoundedTextHistoryProviderFactory : IFileEditHistoryProviderFactory
{
    /// <summary>Acts as a generic fallback so file-type-specific factories can override it.</summary>
    public int Priority => -100;

    public bool CanCreate(FileInteractionProfileDescriptor profile, FileInteractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(request);
        return profile.History.Enabled
            && (profile.Capabilities & (FileInteractionCapabilities.Undo | FileInteractionCapabilities.Redo)) != 0;
    }

    public ValueTask<IFileEditHistoryProvider?> CreateAsync(
        FileInteractionProfileDescriptor profile,
        FileInteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IFileEditHistoryProvider?>(
            CanCreate(profile, request) ? new BoundedTextHistoryProvider(profile.History) : null);
    }
}

public sealed class FileEditHistoryFactoryAmbiguityException : InvalidOperationException
{
    public FileEditHistoryFactoryAmbiguityException(int matchingFactoryCount, int priority = 0)
        : base($"{matchingFactoryCount} history factories matched the same interaction request at priority {priority}.")
    {
        MatchingFactoryCount = matchingFactoryCount;
        Priority = priority;
    }

    public int MatchingFactoryCount { get; }

    public int Priority { get; }
}

/// <summary>Deterministically selects zero or one explicitly registered history factory.</summary>
public sealed class FileEditHistoryProviderCatalog
{
    private readonly IReadOnlyList<IFileEditHistoryProviderFactory> factories;

    public FileEditHistoryProviderCatalog(IEnumerable<IFileEditHistoryProviderFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        this.factories = Array.AsReadOnly(factories
            .Select(factory => factory ?? throw new ArgumentException("Factories cannot contain null values.", nameof(factories)))
            .ToArray());
    }

    public IReadOnlyList<IFileEditHistoryProviderFactory> Factories => factories;

    public async ValueTask<IFileEditHistoryProvider?> CreateAsync(
        FileInteractionProfileDescriptor profile,
        FileInteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = factories.Where(factory => factory.CanCreate(profile, request)).ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        var priority = matches.Max(factory => factory.Priority);
        var finalists = matches
            .Where(factory => factory.Priority == priority)
            .OrderBy(factory => factory.GetType().FullName, StringComparer.Ordinal)
            .ToArray();
        if (finalists.Length > 1)
        {
            throw new FileEditHistoryFactoryAmbiguityException(finalists.Length, priority);
        }

        return await finalists[0].CreateAsync(profile, request, cancellationToken).ConfigureAwait(false);
    }
}
