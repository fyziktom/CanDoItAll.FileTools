namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Owns search strategy selection, continuation lifecycle, and page merging.</summary>
public sealed class FileBrowserSearchCoordinator
{
    private readonly FileBrowserSearchStrategyCatalog strategies;
    private readonly FileBrowserLoader loader;
    private readonly FileBrowserSessionOptions options;
    private readonly HashSet<string> continuationTokens = new(StringComparer.Ordinal);
    private FileBrowserSearchRequest? request;
    private FileBrowserSearchPage? page;
    private IReadOnlyList<FileBrowserItem> visibleItems = [];

    public FileBrowserSearchCoordinator(
        FileBrowserLoader loader,
        FileBrowserSearchStrategyCatalog? strategies = null,
        FileBrowserSessionOptions? options = null)
    {
        this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
        this.strategies = strategies ?? FileBrowserSearchStrategyCatalog.CreateDefault();
        this.options = options ?? new FileBrowserSessionOptions();
    }

    public bool IsActive => request is not null && page is not null;

    public IReadOnlyList<FileBrowserItem> VisibleItems => visibleItems;

    public IReadOnlyList<FileBrowserPageWarning> Warnings => page?.Warnings ?? [];

    public string? NextContinuationToken => page?.NextContinuationToken;

    public long? TotalCount => page?.TotalCount;

    public string? ConsistencyToken => page?.ConsistencyToken;

    public FileBrowserSearchRequest? Request => request;

    public FileBrowserSearchSnapshot? Snapshot => request is null || page is null
        ? null
        : new FileBrowserSearchSnapshot(
            request.Query,
            request.Scope,
            page.StrategyId,
            page.IsPartial,
            page.ScannedContainers,
            page.ScannedItems,
            page.NextContinuationToken,
            page.TotalCount);

    public IReadOnlyList<FileBrowserSearchScope> GetAvailable(IFileBrowserProvider provider)
        => strategies.GetAvailable(provider);

    public async ValueTask SearchAsync(
        IFileBrowserProvider provider,
        FileBrowserLocation location,
        FileBrowserContainerSnapshot activeContainer,
        string query,
        FileBrowserSearchScope scope,
        FileBrowserSortDescriptor sort,
        FileBrowserFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(activeContainer);
        IFileBrowserSearchStrategy strategy = strategies.Get(scope, provider);
        var nextRequest = new FileBrowserSearchRequest(
            location.Key,
            query,
            scope,
            Math.Min(options.PageSize, provider.Descriptor.MaximumPageSize),
            sort: sort,
            filter: filter,
            consistencyToken: activeContainer.ConsistencyToken,
            metadata: options.Metadata);
        var data = new SearchData(loader, provider, activeContainer);
        FileBrowserSearchPage nextPage = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, nextRequest),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        request = nextRequest;
        page = nextPage;
        visibleItems = nextPage.Items;
        continuationTokens.Clear();
        Register(nextPage.NextContinuationToken);
    }

    public async ValueTask LoadMoreAsync(
        IFileBrowserProvider provider,
        FileBrowserContainerSnapshot activeContainer,
        CancellationToken cancellationToken = default)
    {
        if (request is null || page?.NextContinuationToken is not { } token)
        {
            return;
        }

        IFileBrowserSearchStrategy strategy = strategies.Get(request.Scope, provider);
        FileBrowserSearchRequest nextRequest = request.Next(token, page.ConsistencyToken);
        var data = new SearchData(loader, provider, activeContainer);
        FileBrowserSearchPage incoming = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, nextRequest),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        FileBrowserSearchPage merged = Merge(page, visibleItems, incoming, continuationTokens);
        request = nextRequest;
        page = merged;
        visibleItems = merged.Items;
        Register(incoming.NextContinuationToken);
    }

    public void Clear()
    {
        request = null;
        page = null;
        visibleItems = [];
        continuationTokens.Clear();
    }

    internal FileBrowserSearchCheckpoint Capture()
        => new(
            request,
            page,
            visibleItems,
            continuationTokens.ToHashSet(StringComparer.Ordinal));

    internal void Restore(FileBrowserSearchCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        request = checkpoint.Request;
        page = checkpoint.Page;
        visibleItems = checkpoint.VisibleItems;
        continuationTokens.Clear();
        continuationTokens.UnionWith(checkpoint.ContinuationTokens);
    }

    private void Register(string? token)
    {
        if (token is not null)
        {
            continuationTokens.Add(token);
        }
    }

    private static FileBrowserSearchPage Merge(
        FileBrowserSearchPage existing,
        IReadOnlyList<FileBrowserItem> currentItems,
        FileBrowserSearchPage incoming,
        IReadOnlySet<string> observedTokens)
    {
        if (!string.Equals(existing.StrategyId, incoming.StrategyId, StringComparison.Ordinal))
        {
            throw FileBrowserProviderResponseValidator.CorruptProviderResponse(
                "The provider changed search strategy identifiers while paging one result.");
        }

        FileBrowserProviderResponseValidator.ValidateCursorNotPreviouslyObserved(
            incoming.NextContinuationToken,
            observedTokens);
        FileBrowserProviderResponseValidator.ValidateNoConflictingOverlaps(currentItems, incoming.Items);
        var mergedItems = currentItems.ToList();
        HashSet<FileBrowserItemKey> observedKeys = mergedItems.Select(item => item.Key).ToHashSet();
        mergedItems.AddRange(incoming.Items.Where(item => observedKeys.Add(item.Key)));
        if (existing.TotalCount.HasValue
            && incoming.TotalCount.HasValue
            && existing.TotalCount.Value != incoming.TotalCount.Value)
        {
            throw FileBrowserProviderResponseValidator.CorruptProviderResponse(
                "The provider changed the total count while paging a stable search result.");
        }

        long? totalCount = incoming.TotalCount ?? existing.TotalCount;
        if (totalCount.HasValue && totalCount.Value < mergedItems.Count)
        {
            throw FileBrowserProviderResponseValidator.CorruptProviderResponse(
                "The provider returned a total count smaller than the accumulated search result.");
        }

        return new FileBrowserSearchPage(
            mergedItems,
            existing.StrategyId,
            incoming.NextContinuationToken,
            totalCount,
            existing.IsPartial || incoming.IsPartial,
            Math.Max(existing.ScannedContainers, incoming.ScannedContainers),
            Math.Max(existing.ScannedItems, incoming.ScannedItems),
            incoming.ConsistencyToken ?? existing.ConsistencyToken,
            FileBrowserProviderResponseValidator.MergeWarnings(existing.Warnings, incoming.Warnings));
    }

    private sealed class SearchData : IFileBrowserSearchData
    {
        private readonly FileBrowserLoader loader;
        private readonly IFileBrowserProvider provider;
        private readonly FileBrowserContainerSnapshot active;
        private readonly Dictionary<FileBrowserContainerQueryKey, FileBrowserContainerAccumulator> traversal = [];

        public SearchData(
            FileBrowserLoader loader,
            IFileBrowserProvider provider,
            FileBrowserContainerSnapshot active)
        {
            this.loader = loader;
            this.provider = provider;
            this.active = active;
        }

        public IReadOnlyList<FileBrowserItem> CurrentItems => active.Items;

        public bool TryGetItem(FileBrowserItemKey key, out FileBrowserItem? item)
        {
            item = active.Items.FirstOrDefault(candidate => candidate.Key == key);
            return item is not null || loader.StateStore.TryGetItem(key, out item);
        }

        public IReadOnlyList<FileBrowserItem> GetLoadedChildren(FileBrowserItemKey parentKey)
        {
            IEnumerable<FileBrowserItem> current = active.QueryKey.ParentKey == parentKey
                ? active.Items
                : [];
            return current.Concat(loader.StateStore.GetLoadedChildren(parentKey))
                .GroupBy(item => item.Key)
                .Select(group => group.Last())
                .ToArray();
        }

        public IReadOnlyList<FileBrowserItem> GetLoadedDescendants(FileBrowserItemKey parentKey)
        {
            var visited = new HashSet<FileBrowserItemKey> { parentKey };
            var queue = new Queue<FileBrowserItemKey>();
            var result = new List<FileBrowserItem>();
            queue.Enqueue(parentKey);
            while (queue.Count > 0)
            {
                foreach (FileBrowserItem item in GetLoadedChildren(queue.Dequeue()))
                {
                    if (!visited.Add(item.Key))
                    {
                        continue;
                    }

                    result.Add(item);
                    if (item.IsContainer)
                    {
                        queue.Enqueue(item.Key);
                    }
                }
            }

            return result;
        }

        public async ValueTask<FileBrowserPage> BrowseAndCacheAsync(
            FileBrowserBrowseRequest request,
            FileBrowserPageApplyMode applyMode,
            CancellationToken cancellationToken = default)
        {
            FileBrowserPage result = await loader.BrowseForSearchAsync(
                provider,
                request,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var key = new FileBrowserContainerQueryKey(
                request.ParentKey,
                FileBrowserQueryFingerprint.From(request));
            if (applyMode == FileBrowserPageApplyMode.Replace)
            {
                traversal[key] = FileBrowserContainerAccumulator.Start(request, result);
            }
            else if (traversal.TryGetValue(key, out FileBrowserContainerAccumulator? accumulator))
            {
                accumulator.ApplyPage(request, result, applyMode);
            }
            else
            {
                throw new FileBrowserProviderException(new FileBrowserError(
                    FileBrowserErrorCode.StaleCursor,
                    "The first search traversal page is no longer available.",
                    isRetryable: true));
            }

            return result;
        }
    }
}

internal sealed record FileBrowserSearchCheckpoint(
    FileBrowserSearchRequest? Request,
    FileBrowserSearchPage? Page,
    IReadOnlyList<FileBrowserItem> VisibleItems,
    IReadOnlySet<string> ContinuationTokens);
