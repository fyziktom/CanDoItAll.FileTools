namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>One active accumulated container and its reusable first-page request.</summary>
public sealed record FileBrowserLoadedContainer(
    FileBrowserBrowseRequest Request,
    FileBrowserContainerAccumulator Accumulator,
    bool WasRetained)
{
    public FileBrowserContainerSnapshot Snapshot => Accumulator.Snapshot();
}

/// <summary>Owns browse provider I/O, validation, accumulation, and retention hand-off.</summary>
public sealed class FileBrowserLoader
{
    private readonly IFileBrowserStateStore stateStore;
    private readonly FileBrowserSessionOptions options;

    public FileBrowserLoader(
        IFileBrowserStateStore stateStore,
        FileBrowserSessionOptions? options = null)
    {
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.options = options ?? new FileBrowserSessionOptions();
    }

    public IFileBrowserStateStore StateStore => stateStore;

    public async ValueTask<FileBrowserLoadedContainer> LoadAsync(
        IFileBrowserProvider provider,
        FileBrowserLocation location,
        FileBrowserSortDescriptor sort,
        FileBrowserFilter filter,
        bool includeDescendants,
        bool force,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(location);
        FileBrowserBrowseRequest request = CreateRequest(
            provider,
            location.Key,
            sort,
            filter,
            includeDescendants);
        if (!force
            && stateStore.TryGetContainer(request, out FileBrowserContainerSnapshot? retained)
            && retained is not null
            && retained.LoadedPageCount > 0)
        {
            return new FileBrowserLoadedContainer(
                request.FirstPage(),
                FileBrowserContainerAccumulator.FromSnapshot(retained),
                WasRetained: true);
        }

        FileBrowserPage page = await provider.BrowseAsync(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        FileBrowserContainerAccumulator accumulator = FileBrowserContainerAccumulator.Start(request, page);
        cancellationToken.ThrowIfCancellationRequested();
        stateStore.StoreContainer(accumulator.Snapshot());
        return new FileBrowserLoadedContainer(request.FirstPage(), accumulator, WasRetained: false);
    }

    public async ValueTask<FileBrowserLoadedContainer> LoadMoreAsync(
        IFileBrowserProvider provider,
        FileBrowserLoadedContainer active,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(active);
        FileBrowserContainerSnapshot current = active.Snapshot;
        if (current.NextContinuationToken is not { } continuationToken)
        {
            return active;
        }

        FileBrowserBrowseRequest request = active.Request.Next(
            continuationToken,
            current.ConsistencyToken);
        FileBrowserPage page = await provider.BrowseAsync(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        FileBrowserContainerAccumulator accumulator = active.Accumulator.Clone();
        accumulator.ApplyPage(request, page, FileBrowserPageApplyMode.Append);
        cancellationToken.ThrowIfCancellationRequested();
        stateStore.StoreContainer(accumulator.Snapshot());
        return new FileBrowserLoadedContainer(active.Request, accumulator, WasRetained: false);
    }

    public void CommitLocation(FileBrowserLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        stateStore.SetProtectedPath(location.Path.Select(item => item.Key));
        stateStore.StorePath(location.Path);
    }

    public FileBrowserItem ResolveLoadedItem(
        FileBrowserItemKey itemKey,
        IReadOnlyList<FileBrowserItem> activeItems)
    {
        ArgumentNullException.ThrowIfNull(activeItems);
        FileBrowserItem? item = activeItems.FirstOrDefault(candidate => candidate.Key == itemKey);
        if (item is null && !stateStore.TryGetItem(itemKey, out item))
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.NotFound,
                "The item is not loaded in this file browser session."));
        }

        return item!;
    }

    public void InvalidateItem(FileBrowserItemKey itemKey) => stateStore.InvalidateItem(itemKey);

    public void InvalidateSource(FileBrowserSourceId sourceId) => stateStore.InvalidateSource(sourceId);

    public void InvalidateAll() => stateStore.InvalidateAll();

    public FileBrowserTreeDiagnostics GetDiagnostics() => stateStore.GetDiagnostics();

    internal async ValueTask<FileBrowserPage> BrowseForSearchAsync(
        IFileBrowserProvider provider,
        FileBrowserBrowseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ParentKey.SourceId != provider.Descriptor.Id)
        {
            throw new ArgumentException("A search cannot browse another source.", nameof(request));
        }

        FileBrowserPage page = await provider.BrowseAsync(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return page;
    }

    private FileBrowserBrowseRequest CreateRequest(
        IFileBrowserProvider provider,
        FileBrowserItemKey parentKey,
        FileBrowserSortDescriptor sort,
        FileBrowserFilter filter,
        bool includeDescendants)
        => new(
            parentKey,
            Math.Min(options.PageSize, provider.Descriptor.MaximumPageSize),
            sort: sort,
            filter: filter,
            includeDescendants: includeDescendants,
            consistencyToken: parentKey.Revision,
            metadata: options.Metadata);
}
