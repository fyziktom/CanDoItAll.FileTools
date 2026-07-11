namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>
/// Mutable browser workspace composed from focused owners. It coordinates modes and commit order
/// but performs no provider I/O itself; loader, navigator, search, and action collaborators do.
/// </summary>
internal sealed class FileBrowserSessionRuntime
{
    private readonly FileBrowserSessionOptions options;
    private readonly FileBrowserLoader loader;
    private readonly FileBrowserNavigator navigator;
    private readonly FileBrowserSearchCoordinator search;
    private readonly FileBrowserSelectionState selection;
    private readonly FileBrowserBrowseWorkspace workspace;
    private readonly FileBrowserModeCoordinator mode;
    private readonly FileBrowserSourceTransitionCoordinator sourceTransitions;
    private FileBrowserSourceSet sources;

    public FileBrowserSessionRuntime(
        FileBrowserSourceSet sources,
        FileBrowserSearchStrategyCatalog? searchStrategies,
        FileBrowserSessionOptions options,
        IFileBrowserStateStore stateStore,
        FileBrowserNavigator? navigator,
        FileBrowserSelectionState? selection,
        FileBrowserActionDispatcher? actions)
    {
        this.sources = sources;
        this.options = options;
        loader = new FileBrowserLoader(stateStore, options);
        this.navigator = navigator ?? new FileBrowserNavigator();
        this.selection = selection ?? new FileBrowserSelectionState();
        search = new FileBrowserSearchCoordinator(loader, searchStrategies, options);
        workspace = new FileBrowserBrowseWorkspace(options.DefaultSort);
        mode = new FileBrowserModeCoordinator(loader, search, this.selection, workspace, this.navigator);
        sourceTransitions = new FileBrowserSourceTransitionCoordinator(options);
        ActionCoordinator = new FileBrowserSessionActionCoordinator(
            loader,
            actions ?? new FileBrowserActionDispatcher(),
            workspace,
            NavigateAsync,
            () => this.navigator.Current?.Key,
            () => mode.CurrentItems);
    }

    public FileBrowserSessionActionCoordinator ActionCoordinator { get; }

    public FileBrowserSourceId DefaultSourceId
        => sources.Sources.Count > 0
            ? sources.Sources[0].Id
            : throw new InvalidOperationException("The file browser source set is empty.");

    public bool ContainsSource(FileBrowserSourceId sourceId) => sources.TryGet(sourceId, out _);

    public async ValueTask InitializeAsync(
        FileBrowserSourceId sourceId,
        FileBrowserItemKey? startAt,
        CancellationToken cancellationToken)
    {
        FileBrowserNavigationTarget target = await navigator.ResolveInitialAsync(
            sources,
            sourceId,
            startAt,
            options.Metadata,
            cancellationToken);
        FileBrowserLoadedContainer loaded = await loader.LoadAsync(
            target.Provider,
            target.Location,
            options.DefaultSort,
            FileBrowserFilter.None,
            includeDescendants: false,
            force: false,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        workspace.Provider = target.Provider;
        workspace.Container = loaded;
        loader.CommitLocation(target.Location);
        navigator.Reset(target.Location);
        workspace.Sort = options.DefaultSort;
        workspace.Filter = FileBrowserFilter.None;
        workspace.IncludeDescendants = false;
        workspace.ActiveInvalidated = false;
        mode.ClearSearch();
        selection.Clear();
    }

    public async ValueTask NavigateAsync(
        FileBrowserItemKey containerKey,
        CancellationToken cancellationToken)
    {
        if (workspace.Provider?.Descriptor.Id != containerKey.SourceId)
        {
            await InitializeAsync(containerKey.SourceId, containerKey, cancellationToken);
            return;
        }

        FileBrowserNavigationTarget target = await navigator.ResolveAsync(
            sources,
            containerKey,
            options.Metadata,
            cancellationToken);
        FileBrowserLoadedContainer loaded = await loader.LoadAsync(
            target.Provider,
            target.Location,
            workspace.Sort,
            workspace.Filter,
            workspace.IncludeDescendants,
            force: false,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        workspace.Container = loaded;
        loader.CommitLocation(target.Location);
        navigator.Navigate(target.Location);
        workspace.ActiveInvalidated = false;
        mode.ClearSearch();
        selection.Clear();
    }

    public ValueTask GoBackAsync(CancellationToken cancellationToken)
        => NavigateHistoryAsync(navigator.PeekBack, navigator.CommitBack, cancellationToken);

    public ValueTask GoForwardAsync(CancellationToken cancellationToken)
        => NavigateHistoryAsync(navigator.PeekForward, navigator.CommitForward, cancellationToken);

    public ValueTask GoUpAsync(CancellationToken cancellationToken)
        => NavigateHistoryAsync(navigator.PeekUp, navigator.CommitUp, cancellationToken);

    public ValueTask LoadMoreAsync(CancellationToken cancellationToken)
        => mode.LoadMoreAsync(cancellationToken);

    public ValueTask RefreshAsync(CancellationToken cancellationToken)
        => mode.RefreshAsync(cancellationToken);

    public ValueTask SetSortAsync(
        FileBrowserSortDescriptor descriptor,
        CancellationToken cancellationToken)
        => mode.SetSortAsync(descriptor, cancellationToken);

    public ValueTask SetFilterAsync(FileBrowserFilter value, CancellationToken cancellationToken)
        => mode.SetFilterAsync(value, cancellationToken);

    public ValueTask SetIncludeDescendantsAsync(bool value, CancellationToken cancellationToken)
        => mode.SetIncludeDescendantsAsync(value, cancellationToken);

    public ValueTask SearchAsync(
        string query,
        FileBrowserSearchScope scope,
        CancellationToken cancellationToken)
        => mode.SearchAsync(query, scope, cancellationToken);

    public void ClearSearch() => mode.ClearSearch();

    public void Select(FileBrowserItemKey itemKey, bool toggle)
        => selection.Select(mode.CurrentItems, itemKey, toggle);

    public bool ClearSelection() => selection.Clear();

    public void InvalidateItem(FileBrowserItemKey itemKey) => mode.InvalidateItem(itemKey);

    public void InvalidateSource(FileBrowserSourceId sourceId) => mode.InvalidateSource(sourceId);

    public void InvalidateAll() => mode.InvalidateAll();

    public async ValueTask UpdateSourcesAsync(
        FileBrowserSourceSet updatedSources,
        CancellationToken cancellationToken)
    {
        FileBrowserSourceId? oldSource = workspace.Provider?.Descriptor.Id;
        FileBrowserItemKey? oldLocation = navigator.Current?.Key;
        FileBrowserStagedSourceTransition transition = await sourceTransitions.StageAsync(
            updatedSources,
            oldSource,
            oldLocation,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        CommitSourceTransition(transition);
    }

    public FileBrowserSnapshot CreateSnapshot(
        FileBrowserOperationKind operation,
        FileBrowserError? error,
        long revision)
    {
        FileBrowserContainerSnapshot? active = workspace.Container?.Snapshot;
        return new FileBrowserSnapshot(
            sources.Sources,
            workspace.Provider?.Descriptor,
            navigator.Current,
            mode.CurrentItems,
            selection.Snapshot(),
            workspace.Sort,
            workspace.Filter,
            workspace.IncludeDescendants,
            workspace.Provider is null ? [] : search.GetAvailable(workspace.Provider),
            search.Snapshot,
            operation,
            error,
            search.IsActive ? search.Warnings : active?.Warnings ?? [],
            search.IsActive ? search.NextContinuationToken : active?.NextContinuationToken,
            search.IsActive ? search.TotalCount : active?.TotalCount,
            navigator.CanGoBack,
            navigator.CanGoForward,
            navigator.CanGoUp,
            loader.GetDiagnostics(),
            revision,
            search.IsActive ? FileBrowserCompleteness.Unknown : active?.Completeness ?? FileBrowserCompleteness.Unknown,
            search.IsActive ? search.ConsistencyToken : active?.ConsistencyToken);
    }

    public FileBrowserRuntimeCheckpoint Capture()
        => new(
            sources,
            workspace.Provider,
            workspace.Container,
            workspace.Sort,
            workspace.Filter,
            workspace.IncludeDescendants,
            workspace.ActiveInvalidated,
            workspace.SearchInvalidated,
            navigator.Capture(),
            search.Capture(),
            selection.Snapshot());

    public void Restore(FileBrowserRuntimeCheckpoint checkpoint)
    {
        sources = checkpoint.Sources;
        workspace.Provider = checkpoint.Provider;
        workspace.Container = checkpoint.Container;
        workspace.Sort = checkpoint.Sort;
        workspace.Filter = checkpoint.Filter;
        workspace.IncludeDescendants = checkpoint.IncludeDescendants;
        workspace.ActiveInvalidated = checkpoint.ActiveInvalidated;
        workspace.SearchInvalidated = checkpoint.SearchInvalidated;
        navigator.Restore(checkpoint.Navigation);
        search.Restore(checkpoint.Search);
        selection.Restore(checkpoint.SelectedKeys);
        loader.StateStore.SetProtectedPath(navigator.Current?.Path.Select(item => item.Key) ?? []);
    }

    private ValueTask NavigateHistoryAsync(
        Func<FileBrowserLocation> peek,
        Action commit,
        CancellationToken cancellationToken)
        => NavigateHistoryCoreAsync(peek(), commit, cancellationToken);

    private async ValueTask NavigateHistoryCoreAsync(
        FileBrowserLocation location,
        Action commit,
        CancellationToken cancellationToken)
    {
        FileBrowserLoadedContainer loaded = await loader.LoadAsync(
            RequireProvider(),
            location,
            workspace.Sort,
            workspace.Filter,
            workspace.IncludeDescendants,
            force: false,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        workspace.Container = loaded;
        loader.CommitLocation(location);
        commit();
        workspace.ActiveInvalidated = false;
        ClearSearch();
        selection.Clear();
    }

    private void CommitSourceTransition(FileBrowserStagedSourceTransition transition)
    {
        loader.InvalidateAll();
        ResetActiveState();
        sources = transition.Sources;
        if (transition.Target is not { } target || transition.Container is not { } container)
        {
            return;
        }

        loader.StateStore.StoreContainer(container.Snapshot);
        loader.CommitLocation(target.Location);
        workspace.Provider = target.Provider;
        workspace.Container = container;
        workspace.Sort = options.DefaultSort;
        workspace.Filter = FileBrowserFilter.None;
        workspace.IncludeDescendants = false;
        workspace.ActiveInvalidated = false;
        navigator.Reset(target.Location);
        mode.ClearSearch();
        selection.Clear();
    }

    private void ResetActiveState()
    {
        workspace.Reset(options.DefaultSort);
        navigator.Clear();
        search.Clear();
        selection.Clear();
    }

    private IFileBrowserProvider RequireProvider()
        => workspace.Provider ?? throw new InvalidOperationException("The file browser session is not initialized.");

    private FileBrowserLocation RequireLocation()
        => navigator.Current ?? throw new InvalidOperationException("The file browser session is not initialized.");

    private FileBrowserLoadedContainer RequireActiveContainer()
        => workspace.Container ?? throw new InvalidOperationException("The file browser session is not initialized.");

}

internal sealed record FileBrowserRuntimeCheckpoint(
    FileBrowserSourceSet Sources,
    IFileBrowserProvider? Provider,
    FileBrowserLoadedContainer? Container,
    FileBrowserSortDescriptor Sort,
    FileBrowserFilter Filter,
    bool IncludeDescendants,
    bool ActiveInvalidated,
    bool SearchInvalidated,
    FileBrowserNavigationCheckpoint Navigation,
    FileBrowserSearchCheckpoint Search,
    IReadOnlySet<FileBrowserItemKey> SelectedKeys);

internal sealed record FileBrowserExecutedAction(
    FileBrowserActionResult Result,
    bool NavigationCommitted);

