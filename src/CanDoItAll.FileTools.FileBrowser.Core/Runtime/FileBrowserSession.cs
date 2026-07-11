namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>
/// Thin serialized facade over focused browser collaborators. It owns lifecycle, cancellation,
/// retry/checkpoints, and immutable snapshot publication; it performs no provider I/O.
/// </summary>
public sealed class FileBrowserSession : IFileBrowserSession
{
    private readonly FileBrowserSessionRuntime runtime;
    private readonly FileBrowserSessionExecutionCoordinator execution;
    private long revision;

    public FileBrowserSession(
        IFileBrowserProviderCatalog providerCatalog,
        FileBrowserSearchStrategyCatalog? searchStrategies = null,
        FileBrowserSessionOptions? options = null)
        : this(
            new FileBrowserSourceSet(
                "initial",
                (providerCatalog ?? throw new ArgumentNullException(nameof(providerCatalog)))
                    .Sources.Select(source => providerCatalog.Get(source.Id))),
            searchStrategies,
            options)
    {
    }

    public FileBrowserSession(
        IEnumerable<IFileBrowserProvider> providers,
        FileBrowserSessionOptions? options = null)
        : this(new FileBrowserProviderCatalog(providers), options: options)
    {
    }

    public FileBrowserSession(
        FileBrowserSourceSet sources,
        FileBrowserSearchStrategyCatalog? searchStrategies = null,
        FileBrowserSessionOptions? options = null,
        IFileBrowserStateStore? stateStore = null,
        FileBrowserNavigator? navigator = null,
        FileBrowserSelectionState? selection = null,
        FileBrowserActionDispatcher? actions = null)
    {
        var configuredOptions = options ?? new FileBrowserSessionOptions();
        runtime = new FileBrowserSessionRuntime(
            sources ?? throw new ArgumentNullException(nameof(sources)),
            searchStrategies,
            configuredOptions,
            stateStore ?? FileBrowserStateStoreFactory.Create(configuredOptions),
            navigator,
            selection,
            actions);
        execution = new FileBrowserSessionExecutionCoordinator(runtime, Publish);
        Snapshot = runtime.CreateSnapshot(execution.Operation, execution.Error, ++revision);
    }

    public event EventHandler<FileBrowserSnapshotChangedEventArgs>? Changed;

    public FileBrowserSnapshot Snapshot { get; private set; }

    public ValueTask InitializeAsync(
        FileBrowserSourceId? sourceId = null,
        FileBrowserItemKey? startAt = null,
        CancellationToken cancellationToken = default)
    {
        FileBrowserSourceId selectedSource = sourceId ?? runtime.DefaultSourceId;
        if (startAt.HasValue && startAt.Value.SourceId != selectedSource)
        {
            throw new ArgumentException("The starting item must belong to the selected source.", nameof(startAt));
        }

        return execution.ExecuteAsync(
            FileBrowserOperationKind.Initializing,
            token => runtime.InitializeAsync(selectedSource, startAt, token),
            cancellationToken);
    }

    public ValueTask ChangeSourceAsync(FileBrowserSourceId sourceId, CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(
            FileBrowserOperationKind.Initializing,
            token => runtime.InitializeAsync(sourceId, null, token),
            cancellationToken);

    public ValueTask NavigateAsync(FileBrowserItemKey containerKey, CancellationToken cancellationToken = default)
    {
        if (!runtime.ContainsSource(containerKey.SourceId))
        {
            throw new KeyNotFoundException($"File browser source '{containerKey.SourceId}' is not registered.");
        }

        return execution.ExecuteAsync(
            FileBrowserOperationKind.LoadingFolder,
            token => runtime.NavigateAsync(containerKey, token),
            cancellationToken);
    }

    public ValueTask GoBackAsync(CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(FileBrowserOperationKind.LoadingFolder, runtime.GoBackAsync, cancellationToken);

    public ValueTask GoForwardAsync(CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(FileBrowserOperationKind.LoadingFolder, runtime.GoForwardAsync, cancellationToken);

    public ValueTask GoUpAsync(CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(FileBrowserOperationKind.LoadingFolder, runtime.GoUpAsync, cancellationToken);

    public ValueTask LoadMoreAsync(CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(FileBrowserOperationKind.LoadingMore, runtime.LoadMoreAsync, cancellationToken);

    public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(FileBrowserOperationKind.Refreshing, runtime.RefreshAsync, cancellationToken);

    public ValueTask RetryAsync(CancellationToken cancellationToken = default)
        => execution.RetryAsync(cancellationToken);

    public ValueTask SetSortAsync(FileBrowserSortDescriptor sort, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sort);
        return execution.ExecuteAsync(
            FileBrowserOperationKind.Refreshing,
            token => runtime.SetSortAsync(sort, token),
            cancellationToken);
    }

    public ValueTask SetFilterAsync(FileBrowserFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return execution.ExecuteAsync(
            FileBrowserOperationKind.Refreshing,
            token => runtime.SetFilterAsync(filter, token),
            cancellationToken);
    }

    public ValueTask SetIncludeDescendantsAsync(
        bool includeDescendants,
        CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(
            FileBrowserOperationKind.Refreshing,
            token => runtime.SetIncludeDescendantsAsync(includeDescendants, token),
            cancellationToken);

    public ValueTask SearchAsync(
        string query,
        FileBrowserSearchScope scope,
        CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(query)
            ? ClearSearchAsync(cancellationToken)
            : execution.ExecuteAsync(
                FileBrowserOperationKind.Searching,
                token => runtime.SearchAsync(query, scope, token),
                cancellationToken);

    public ValueTask ClearSearchAsync(CancellationToken cancellationToken = default)
        => execution.ExecuteAsync(
            FileBrowserOperationKind.LoadingFolder,
            token =>
            {
                token.ThrowIfCancellationRequested();
                runtime.ClearSearch();
                return ValueTask.CompletedTask;
            },
            cancellationToken);

    public ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
        => execution.ExecuteSerializedAsync(
            token => runtime.ActionCoordinator.GetActionsAsync(itemKey, token),
            cancellationToken);

    public ValueTask<FileBrowserActionResult> ExecuteActionAsync(
        FileBrowserActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return FileBrowserActionExecution.RunAsync(
            () => execution.ExecuteSerializedAsync(
                token => runtime.ActionCoordinator.ExecuteActionAsync(request, token),
                cancellationToken),
            execution.PublishCurrent);
    }

    public ValueTask<FileBrowserContentLease> OpenReadAsync(
        FileBrowserReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return execution.ExecuteSerializedAsync(
            token => runtime.ActionCoordinator.OpenReadAsync(request, token),
            cancellationToken);
    }

    public void Select(FileBrowserItemKey itemKey, bool toggle = false)
    {
        execution.ThrowIfDisposed();
        runtime.Select(itemKey, toggle);
        execution.PublishCurrent();
    }

    public void ClearSelection()
    {
        execution.ThrowIfDisposed();
        if (runtime.ClearSelection())
        {
            execution.PublishCurrent();
        }
    }

    public ValueTask InvalidateItemAsync(
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
        => execution.ExecuteSupersedingAsync(
            busyOperation: null,
            token =>
            {
                token.ThrowIfCancellationRequested();
                runtime.InvalidateItem(itemKey);
                return ValueTask.CompletedTask;
            },
            retryOnFailure: false,
            clearRetryOnSuccess: false,
            cancellationToken);

    public ValueTask InvalidateSourceAsync(
        FileBrowserSourceId sourceId,
        CancellationToken cancellationToken = default)
        => execution.ExecuteSupersedingAsync(
            busyOperation: null,
            token =>
            {
                token.ThrowIfCancellationRequested();
                runtime.InvalidateSource(sourceId);
                return ValueTask.CompletedTask;
            },
            retryOnFailure: false,
            clearRetryOnSuccess: false,
            cancellationToken);

    public ValueTask InvalidateAllAsync(CancellationToken cancellationToken = default)
        => execution.ExecuteSupersedingAsync(
            busyOperation: null,
            token =>
            {
                token.ThrowIfCancellationRequested();
                runtime.InvalidateAll();
                return ValueTask.CompletedTask;
            },
            retryOnFailure: false,
            clearRetryOnSuccess: false,
            cancellationToken);

    public ValueTask UpdateSourcesAsync(
        FileBrowserSourceSet sources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return execution.ExecuteSupersedingAsync(
            FileBrowserOperationKind.Initializing,
            token => runtime.UpdateSourcesAsync(sources, token),
            retryOnFailure: true,
            clearRetryOnSuccess: true,
            cancellationToken);
    }

    private void Publish(FileBrowserOperationKind operation, FileBrowserError? error)
    {
        Snapshot = runtime.CreateSnapshot(operation, error, ++revision);
        Changed?.Invoke(this, new FileBrowserSnapshotChangedEventArgs(Snapshot));
    }

    public ValueTask DisposeAsync()
    {
        Changed = null;
        return execution.DisposeAsync();
    }

}
