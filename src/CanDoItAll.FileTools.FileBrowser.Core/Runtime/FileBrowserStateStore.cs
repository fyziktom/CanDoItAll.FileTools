namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Controls whether completed container snapshots may be reused by a session.</summary>
public enum FileBrowserStateRetentionMode
{
    Disabled,
    Bounded
}

/// <summary>Configures bounded retained browser state.</summary>
public sealed record FileBrowserTreeStoreOptions
{
    public FileBrowserTreeStoreOptions(int maximumContainers = 128, int maximumItems = 10_000)
    {
        if (maximumContainers < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumContainers));
        }

        if (maximumItems < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }

        MaximumContainers = maximumContainers;
        MaximumItems = maximumItems;
    }

    public int MaximumContainers { get; }

    public int MaximumItems { get; }
}

/// <summary>Determines whether an incoming page replaces or extends an accumulated result.</summary>
public enum FileBrowserPageApplyMode
{
    Replace,
    Append
}

/// <summary>Identifies a container and one sort/filter/metadata query over it.</summary>
public readonly record struct FileBrowserContainerQueryKey(
    FileBrowserItemKey ParentKey,
    FileBrowserQueryFingerprint Fingerprint);

/// <summary>Immutable projection of one accumulated container query.</summary>
public sealed record FileBrowserContainerSnapshot(
    FileBrowserContainerQueryKey QueryKey,
    IReadOnlyList<FileBrowserItem> Items,
    string? NextContinuationToken,
    long? TotalCount,
    string? ConsistencyToken,
    FileBrowserCompleteness Completeness,
    IReadOnlyList<FileBrowserPageWarning> Warnings,
    FileBrowserError? Error,
    int LoadedPageCount,
    DateTimeOffset LastAccessedAt)
{
    internal FileBrowserContinuationHistory ContinuationHistory { get; init; }
        = FileBrowserContinuationHistory.Empty;

    public bool IsComplete => Completeness == FileBrowserCompleteness.Complete
        && NextContinuationToken is null
        && Error is null;

    public bool HasMore => NextContinuationToken is not null;
}

/// <summary>Retention metrics exposed for telemetry and freshness proof.</summary>
public sealed record FileBrowserTreeDiagnostics(
    int CachedItemCount,
    int CachedContainerQueryCount,
    int ProtectedItemCount,
    int EvictedContainerQueryCount)
{
    public static FileBrowserTreeDiagnostics Empty { get; } = new(0, 0, 0, 0);
}

/// <summary>Read-only hierarchy access used by loaded-tree search strategies.</summary>
public interface IFileBrowserLoadedTree
{
    bool TryGetItem(FileBrowserItemKey key, out FileBrowserItem? item);

    IReadOnlyList<FileBrowserItem> GetLoadedChildren(FileBrowserItemKey parentKey);

    IReadOnlyList<FileBrowserItem> GetLoadedDescendants(FileBrowserItemKey parentKey);
}

/// <summary>
/// Retains reusable snapshots only. The active rendered container is owned separately by the
/// session/loader and therefore remains available even when this store is disabled.
/// </summary>
public interface IFileBrowserStateStore : IFileBrowserLoadedTree
{
    FileBrowserStateRetentionMode Mode { get; }

    bool TryGetContainer(
        FileBrowserBrowseRequest request,
        out FileBrowserContainerSnapshot? snapshot);

    void StoreContainer(FileBrowserContainerSnapshot snapshot);

    void StorePath(IReadOnlyList<FileBrowserItem> path);

    void SetProtectedPath(IEnumerable<FileBrowserItemKey> keys);

    void InvalidateItem(FileBrowserItemKey itemKey);

    void InvalidateSource(FileBrowserSourceId sourceId);

    void InvalidateAll();

    FileBrowserTreeDiagnostics GetDiagnostics();
}

internal static class FileBrowserStateStoreFactory
{
    public static IFileBrowserStateStore Create(FileBrowserSessionOptions options)
        => options.RetentionMode == FileBrowserStateRetentionMode.Disabled
            ? new DisabledFileBrowserStateStore()
            : new BoundedFileBrowserStateStore(options.Cache);
}

/// <summary>A true no-retention implementation. Every lookup misses and diagnostics stay zero.</summary>
public sealed class DisabledFileBrowserStateStore : IFileBrowserStateStore
{
    public FileBrowserStateRetentionMode Mode => FileBrowserStateRetentionMode.Disabled;

    public bool TryGetContainer(
        FileBrowserBrowseRequest request,
        out FileBrowserContainerSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        snapshot = null;
        return false;
    }

    public void StoreContainer(FileBrowserContainerSnapshot snapshot)
        => ArgumentNullException.ThrowIfNull(snapshot);

    public void StorePath(IReadOnlyList<FileBrowserItem> path)
        => ArgumentNullException.ThrowIfNull(path);

    public void SetProtectedPath(IEnumerable<FileBrowserItemKey> keys)
        => ArgumentNullException.ThrowIfNull(keys);

    public void InvalidateItem(FileBrowserItemKey itemKey)
    {
    }

    public void InvalidateSource(FileBrowserSourceId sourceId)
    {
    }

    public void InvalidateAll()
    {
    }

    public bool TryGetItem(FileBrowserItemKey key, out FileBrowserItem? item)
    {
        item = null;
        return false;
    }

    public IReadOnlyList<FileBrowserItem> GetLoadedChildren(FileBrowserItemKey parentKey) => [];

    public IReadOnlyList<FileBrowserItem> GetLoadedDescendants(FileBrowserItemKey parentKey) => [];

    public FileBrowserTreeDiagnostics GetDiagnostics() => FileBrowserTreeDiagnostics.Empty;
}

/// <summary>Thread-safe LRU retention for completed paths and container snapshots.</summary>
public sealed class BoundedFileBrowserStateStore : IFileBrowserStateStore
{
    private readonly object sync = new();
    private readonly FileBrowserTreeStoreOptions options;
    private readonly Dictionary<FileBrowserContainerQueryKey, RetainedContainer> containers = [];
    private readonly Dictionary<FileBrowserItemKey, FileBrowserItem> items = [];
    private readonly Dictionary<FileBrowserItemKey, long> itemAccess = [];
    private readonly HashSet<FileBrowserItemKey> protectedPath = [];
    private long accessSequence;
    private int evictedContainers;

    public BoundedFileBrowserStateStore(FileBrowserTreeStoreOptions? options = null)
    {
        this.options = options ?? new FileBrowserTreeStoreOptions();
    }

    public FileBrowserStateRetentionMode Mode => FileBrowserStateRetentionMode.Bounded;

    public bool TryGetContainer(
        FileBrowserBrowseRequest request,
        out FileBrowserContainerSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (sync)
        {
            if (!containers.TryGetValue(CreateQueryKey(request), out RetainedContainer? entry))
            {
                snapshot = null;
                return false;
            }

            entry.LastAccess = NextAccess();
            snapshot = entry.Snapshot with { LastAccessedAt = DateTimeOffset.UtcNow };
            Touch(snapshot.Items);
            return true;
        }
    }

    public void StoreContainer(FileBrowserContainerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (sync)
        {
            var immutable = CopySnapshot(snapshot);
            containers[immutable.QueryKey] = new RetainedContainer(immutable, NextAccess());
            foreach (FileBrowserItem item in immutable.Items)
            {
                Upsert(item);
            }

            Trim();
        }
    }

    public void StorePath(IReadOnlyList<FileBrowserItem> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Count == 0)
        {
            throw new ArgumentException("A path must contain at least one item.", nameof(path));
        }

        FileBrowserSourceId sourceId = path[0].Key.SourceId;
        if (path.Any(item => item.Key.SourceId != sourceId))
        {
            throw new ArgumentException("Every path item must belong to the same source.", nameof(path));
        }

        lock (sync)
        {
            foreach (FileBrowserItem item in path)
            {
                Upsert(item);
            }

            Trim();
        }
    }

    public void SetProtectedPath(IEnumerable<FileBrowserItemKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (sync)
        {
            protectedPath.Clear();
            protectedPath.UnionWith(keys);
            Trim();
        }
    }

    public void InvalidateItem(FileBrowserItemKey itemKey)
    {
        lock (sync)
        {
            foreach (FileBrowserContainerQueryKey key in containers
                         .Where(pair => pair.Key.ParentKey == itemKey
                             || pair.Value.Snapshot.Items.Any(item => item.Key == itemKey))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                containers.Remove(key);
            }

            items.Remove(itemKey);
            itemAccess.Remove(itemKey);
            protectedPath.Remove(itemKey);
            PruneUnreferencedItems();
        }
    }

    public void InvalidateSource(FileBrowserSourceId sourceId)
    {
        lock (sync)
        {
            foreach (FileBrowserContainerQueryKey key in containers.Keys
                         .Where(key => key.ParentKey.SourceId == sourceId)
                         .ToArray())
            {
                containers.Remove(key);
            }

            foreach (FileBrowserItemKey key in items.Keys
                         .Where(key => key.SourceId == sourceId)
                         .ToArray())
            {
                items.Remove(key);
                itemAccess.Remove(key);
            }

            protectedPath.RemoveWhere(key => key.SourceId == sourceId);
        }
    }

    public void InvalidateAll()
    {
        lock (sync)
        {
            containers.Clear();
            items.Clear();
            itemAccess.Clear();
            protectedPath.Clear();
        }
    }

    public bool TryGetItem(FileBrowserItemKey key, out FileBrowserItem? item)
    {
        lock (sync)
        {
            if (!items.TryGetValue(key, out item))
            {
                return false;
            }

            itemAccess[key] = NextAccess();
            return true;
        }
    }

    public IReadOnlyList<FileBrowserItem> GetLoadedChildren(FileBrowserItemKey parentKey)
    {
        lock (sync)
        {
            FileBrowserItem[] result = containers.Values
                .Where(entry => entry.Snapshot.QueryKey.ParentKey == parentKey)
                .SelectMany(entry => entry.Snapshot.Items)
                .GroupBy(item => item.Key)
                .Select(group => group.Last())
                .ToArray();
            Touch(result);
            return result;
        }
    }

    public IReadOnlyList<FileBrowserItem> GetLoadedDescendants(FileBrowserItemKey parentKey)
    {
        lock (sync)
        {
            var visited = new HashSet<FileBrowserItemKey> { parentKey };
            var queue = new Queue<FileBrowserItemKey>();
            var result = new List<FileBrowserItem>();
            queue.Enqueue(parentKey);

            while (queue.Count > 0)
            {
                FileBrowserItemKey current = queue.Dequeue();
                foreach (FileBrowserItem child in containers.Values
                             .Where(entry => entry.Snapshot.QueryKey.ParentKey == current)
                             .SelectMany(entry => entry.Snapshot.Items)
                             .GroupBy(item => item.Key)
                             .Select(group => group.Last()))
                {
                    if (!visited.Add(child.Key))
                    {
                        continue;
                    }

                    result.Add(child);
                    if (child.IsContainer)
                    {
                        queue.Enqueue(child.Key);
                    }
                }
            }

            Touch(result);
            return result;
        }
    }

    public FileBrowserTreeDiagnostics GetDiagnostics()
    {
        lock (sync)
        {
            return new FileBrowserTreeDiagnostics(
                items.Count,
                containers.Count,
                protectedPath.Count,
                evictedContainers);
        }
    }

    private static FileBrowserContainerQueryKey CreateQueryKey(FileBrowserBrowseRequest request)
        => new(request.ParentKey, FileBrowserQueryFingerprint.From(request));

    private static FileBrowserContainerSnapshot CopySnapshot(FileBrowserContainerSnapshot snapshot)
        => snapshot with
        {
            Items = Array.AsReadOnly(snapshot.Items.ToArray()),
            Warnings = Array.AsReadOnly(snapshot.Warnings.ToArray()),
            ContinuationHistory = snapshot.ContinuationHistory,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

    private void Upsert(FileBrowserItem item)
    {
        items[item.Key] = item;
        itemAccess[item.Key] = NextAccess();
    }

    private void Touch(IEnumerable<FileBrowserItem> values)
    {
        foreach (FileBrowserItem item in values)
        {
            itemAccess[item.Key] = NextAccess();
        }
    }

    private long NextAccess() => ++accessSequence;

    private void Trim()
    {
        while (containers.Count > options.MaximumContainers && TryEvictContainer())
        {
        }

        while (items.Count > options.MaximumItems && TryEvictContainer())
        {
            PruneUnreferencedItems();
        }

        if (items.Count > options.MaximumItems)
        {
            var referenced = ReferencedKeys();
            foreach (FileBrowserItemKey key in items.Keys
                         .Where(key => !referenced.Contains(key))
                         .OrderBy(key => itemAccess.GetValueOrDefault(key))
                         .Take(items.Count - options.MaximumItems)
                         .ToArray())
            {
                items.Remove(key);
                itemAccess.Remove(key);
            }
        }
    }

    private bool TryEvictContainer()
    {
        KeyValuePair<FileBrowserContainerQueryKey, RetainedContainer>? candidate = containers
            .Where(pair => !protectedPath.Contains(pair.Key.ParentKey))
            .OrderBy(pair => pair.Value.LastAccess)
            .Cast<KeyValuePair<FileBrowserContainerQueryKey, RetainedContainer>?>()
            .FirstOrDefault();
        if (!candidate.HasValue)
        {
            return false;
        }

        containers.Remove(candidate.Value.Key);
        evictedContainers++;
        return true;
    }

    private void PruneUnreferencedItems()
    {
        HashSet<FileBrowserItemKey> referenced = ReferencedKeys();
        foreach (FileBrowserItemKey key in items.Keys.Where(key => !referenced.Contains(key)).ToArray())
        {
            items.Remove(key);
            itemAccess.Remove(key);
        }
    }

    private HashSet<FileBrowserItemKey> ReferencedKeys()
    {
        var referenced = new HashSet<FileBrowserItemKey>(protectedPath);
        foreach (FileBrowserContainerSnapshot snapshot in containers.Values.Select(value => value.Snapshot))
        {
            referenced.Add(snapshot.QueryKey.ParentKey);
            referenced.UnionWith(snapshot.Items.Select(item => item.Key));
        }

        return referenced;
    }

    private sealed class RetainedContainer(FileBrowserContainerSnapshot snapshot, long lastAccess)
    {
        public FileBrowserContainerSnapshot Snapshot { get; } = snapshot;

        public long LastAccess { get; set; } = lastAccess;
    }
}
