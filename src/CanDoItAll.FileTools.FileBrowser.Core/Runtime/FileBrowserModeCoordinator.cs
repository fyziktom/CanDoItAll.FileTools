namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>
/// Keeps browse and search modes on one coherent sort/filter query while owning refresh,
/// pagination, and invalidation policy.
/// </summary>
internal sealed class FileBrowserModeCoordinator
{
    private readonly FileBrowserLoader loader;
    private readonly FileBrowserSearchCoordinator search;
    private readonly FileBrowserSelectionState selection;
    private readonly FileBrowserBrowseWorkspace workspace;
    private readonly FileBrowserNavigator navigator;

    public FileBrowserModeCoordinator(
        FileBrowserLoader loader,
        FileBrowserSearchCoordinator search,
        FileBrowserSelectionState selection,
        FileBrowserBrowseWorkspace workspace,
        FileBrowserNavigator navigator)
    {
        this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
        this.search = search ?? throw new ArgumentNullException(nameof(search));
        this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        this.navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
    }

    public IReadOnlyList<FileBrowserItem> CurrentItems
        => search.IsActive ? search.VisibleItems : workspace.Container?.Snapshot.Items ?? [];

    public async ValueTask LoadMoreAsync(CancellationToken cancellationToken)
    {
        FileBrowserLoadedContainer active = RequireActiveContainer();
        if (search.IsActive)
        {
            if (workspace.SearchInvalidated)
            {
                throw StaleCursor("The active search was invalidated; refresh it before loading more.");
            }

            await search.LoadMoreAsync(RequireProvider(), active.Snapshot, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            if (workspace.ActiveInvalidated)
            {
                throw StaleCursor("The active folder was invalidated; refresh it before loading more.");
            }

            workspace.Container = await loader.LoadMoreAsync(
                RequireProvider(),
                active,
                cancellationToken).ConfigureAwait(false);
        }

        selection.Reconcile(CurrentItems);
    }

    public async ValueTask RefreshAsync(CancellationToken cancellationToken)
    {
        if (!search.IsActive)
        {
            await LoadLocationAsync(RequireLocation(), force: true, cancellationToken)
                .ConfigureAwait(false);
            selection.Reconcile(CurrentItems);
            return;
        }

        FileBrowserSearchRequest request = search.Request!;
        await LoadLocationAsync(RequireLocation(), force: true, cancellationToken)
            .ConfigureAwait(false);
        await SearchAsync(request.Query, request.Scope, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetSortAsync(
        FileBrowserSortDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!RequireProvider().Descriptor.SupportedSortFields.Contains(descriptor.Field))
        {
            throw Unsupported($"This source cannot sort by {descriptor.Field}.");
        }

        workspace.Sort = descriptor;
        await ReloadCurrentModeAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetFilterAsync(
        FileBrowserFilter value,
        CancellationToken cancellationToken)
    {
        workspace.Filter = value ?? throw new ArgumentNullException(nameof(value));
        await ReloadCurrentModeAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetIncludeDescendantsAsync(
        bool value,
        CancellationToken cancellationToken)
    {
        if (value && !RequireProvider().Descriptor.Supports(FileBrowserSourceCapabilities.RecursiveBrowse))
        {
            throw Unsupported("This source does not support recursive folder listing.");
        }

        workspace.IncludeDescendants = value;
        ClearSearch();
        await LoadLocationAsync(RequireLocation(), force: false, cancellationToken)
            .ConfigureAwait(false);
        selection.Reconcile(CurrentItems);
    }

    public async ValueTask SearchAsync(
        string query,
        FileBrowserSearchScope scope,
        CancellationToken cancellationToken)
    {
        await search.SearchAsync(
            RequireProvider(),
            RequireLocation(),
            RequireActiveContainer().Snapshot,
            query,
            scope,
            workspace.Sort,
            workspace.Filter,
            cancellationToken).ConfigureAwait(false);
        workspace.SearchInvalidated = false;
        selection.Reconcile(CurrentItems);
    }

    public void ClearSearch()
    {
        search.Clear();
        workspace.SearchInvalidated = false;
        selection.Reconcile(CurrentItems);
    }

    public void InvalidateItem(FileBrowserItemKey itemKey)
    {
        loader.InvalidateItem(itemKey);
        bool activeAffected = workspace.Container?.Snapshot.QueryKey.ParentKey == itemKey
            || workspace.Container?.Snapshot.Items.Any(item => item.Key == itemKey) == true;
        workspace.ActiveInvalidated |= activeAffected;
        workspace.SearchInvalidated |= search.IsActive
            && (workspace.ActiveInvalidated
                || search.Request?.ContainerKey == itemKey
                || search.VisibleItems.Any(item => item.Key == itemKey));
    }

    public void InvalidateSource(FileBrowserSourceId sourceId)
    {
        loader.InvalidateSource(sourceId);
        if (workspace.Provider?.Descriptor.Id == sourceId)
        {
            workspace.ActiveInvalidated = true;
            workspace.SearchInvalidated = search.IsActive;
        }
    }

    public void InvalidateAll()
    {
        loader.InvalidateAll();
        workspace.ActiveInvalidated = workspace.Container is not null;
        workspace.SearchInvalidated = search.IsActive;
    }

    private async ValueTask ReloadCurrentModeAsync(CancellationToken cancellationToken)
    {
        if (!search.IsActive)
        {
            await LoadLocationAsync(RequireLocation(), force: false, cancellationToken)
                .ConfigureAwait(false);
            selection.Reconcile(CurrentItems);
            return;
        }

        FileBrowserSearchRequest request = search.Request!;
        await LoadLocationAsync(RequireLocation(), force: false, cancellationToken)
            .ConfigureAwait(false);
        await SearchAsync(request.Query, request.Scope, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask LoadLocationAsync(
        FileBrowserLocation location,
        bool force,
        CancellationToken cancellationToken)
    {
        workspace.Container = await loader.LoadAsync(
            RequireProvider(),
            location,
            workspace.Sort,
            workspace.Filter,
            workspace.IncludeDescendants,
            force,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        loader.CommitLocation(location);
        workspace.ActiveInvalidated = false;
    }

    private IFileBrowserProvider RequireProvider()
        => workspace.Provider ?? throw new InvalidOperationException("The file browser session is not initialized.");

    private FileBrowserLocation RequireLocation()
        => navigator.Current ?? throw new InvalidOperationException("The file browser session is not initialized.");

    private FileBrowserLoadedContainer RequireActiveContainer()
        => workspace.Container ?? throw new InvalidOperationException("The file browser session is not initialized.");

    private static FileBrowserProviderException Unsupported(string message)
        => new(new FileBrowserError(FileBrowserErrorCode.Unsupported, message));

    private static FileBrowserProviderException StaleCursor(string message)
        => new(new FileBrowserError(FileBrowserErrorCode.StaleCursor, message, isRetryable: true));
}
