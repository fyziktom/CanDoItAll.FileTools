namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Current asynchronous operation exposed to renderers.</summary>
public enum FileBrowserOperationKind
{
    Idle,
    Initializing,
    LoadingFolder,
    LoadingMore,
    Refreshing,
    Searching
}

/// <summary>Configures one reusable browser session.</summary>
public sealed record FileBrowserSessionOptions
{
    public FileBrowserSessionOptions(
        int pageSize = 50,
        FileBrowserSortDescriptor? defaultSort = null,
        FileBrowserMetadataRequest? metadata = null,
        FileBrowserTreeStoreOptions? cache = null,
        FileBrowserStateRetentionMode retentionMode = FileBrowserStateRetentionMode.Bounded)
    {
        if (pageSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        if (!Enum.IsDefined(typeof(FileBrowserStateRetentionMode), retentionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(retentionMode));
        }

        PageSize = pageSize;
        DefaultSort = defaultSort ?? new FileBrowserSortDescriptor();
        Metadata = metadata ?? FileBrowserMetadataRequest.Standard;
        Cache = cache ?? new FileBrowserTreeStoreOptions();
        RetentionMode = retentionMode;
    }

    public int PageSize { get; }

    public FileBrowserSortDescriptor DefaultSort { get; }

    public FileBrowserMetadataRequest Metadata { get; }

    public FileBrowserTreeStoreOptions Cache { get; }

    public FileBrowserStateRetentionMode RetentionMode { get; }
}

/// <summary>Immutable projection of an active search.</summary>
public sealed record FileBrowserSearchSnapshot(
    string Query,
    FileBrowserSearchScope Scope,
    string StrategyId,
    bool IsPartial,
    int ScannedContainers,
    int ScannedItems,
    string? NextContinuationToken,
    long? TotalCount);

/// <summary>Immutable renderer-facing browser state.</summary>
public sealed record FileBrowserSnapshot
{
    public FileBrowserSnapshot(
        IReadOnlyList<FileBrowserSourceDescriptor> sources,
        FileBrowserSourceDescriptor? currentSource,
        FileBrowserLocation? location,
        IReadOnlyList<FileBrowserItem> items,
        IReadOnlySet<FileBrowserItemKey> selectedKeys,
        FileBrowserSortDescriptor sort,
        FileBrowserFilter filter,
        bool includeDescendants,
        IReadOnlyList<FileBrowserSearchScope> availableSearchScopes,
        FileBrowserSearchSnapshot? search,
        FileBrowserOperationKind operation,
        FileBrowserError? error,
        IReadOnlyList<FileBrowserPageWarning> warnings,
        string? nextContinuationToken,
        long? totalCount,
        bool canGoBack,
        bool canGoForward,
        bool canGoUp,
        FileBrowserTreeDiagnostics diagnostics,
        long revision,
        FileBrowserCompleteness browseCompleteness = FileBrowserCompleteness.Unknown,
        string? consistencyToken = null)
    {
        Sources = Array.AsReadOnly(sources.ToArray());
        CurrentSource = currentSource;
        Location = location;
        Items = Array.AsReadOnly(items.ToArray());
        SelectedKeys = new HashSet<FileBrowserItemKey>(selectedKeys);
        Sort = sort;
        Filter = filter;
        IncludeDescendants = includeDescendants;
        AvailableSearchScopes = Array.AsReadOnly(availableSearchScopes.ToArray());
        Search = search;
        Operation = operation;
        Error = error;
        Warnings = Array.AsReadOnly(warnings.ToArray());
        NextContinuationToken = nextContinuationToken;
        TotalCount = totalCount;
        CanGoBack = canGoBack;
        CanGoForward = canGoForward;
        CanGoUp = canGoUp;
        Diagnostics = diagnostics;
        Revision = revision;
        BrowseCompleteness = browseCompleteness;
        ConsistencyToken = string.IsNullOrWhiteSpace(consistencyToken) ? null : consistencyToken;
    }

    public IReadOnlyList<FileBrowserSourceDescriptor> Sources { get; }

    public FileBrowserSourceDescriptor? CurrentSource { get; }

    public FileBrowserLocation? Location { get; }

    public FileBrowserItem? CurrentContainer => Location?.Current;

    public IReadOnlyList<FileBrowserItem> Items { get; }

    public IReadOnlySet<FileBrowserItemKey> SelectedKeys { get; }

    public FileBrowserSortDescriptor Sort { get; }

    public FileBrowserFilter Filter { get; }

    public bool IncludeDescendants { get; }

    public IReadOnlyList<FileBrowserSearchScope> AvailableSearchScopes { get; }

    public FileBrowserSearchSnapshot? Search { get; }

    public FileBrowserOperationKind Operation { get; }

    public bool IsBusy => Operation != FileBrowserOperationKind.Idle;

    public FileBrowserError? Error { get; }

    public IReadOnlyList<FileBrowserPageWarning> Warnings { get; }

    public string? NextContinuationToken { get; }

    public bool HasMore => NextContinuationToken is not null;

    public long? TotalCount { get; }

    public bool CanGoBack { get; }

    public bool CanGoForward { get; }

    public bool CanGoUp { get; }

    public FileBrowserTreeDiagnostics Diagnostics { get; }

    public long Revision { get; }

    /// <summary>Gets completeness for the active browse page set; search partiality is exposed by <see cref="Search"/>.</summary>
    public FileBrowserCompleteness BrowseCompleteness { get; }

    /// <summary>Gets the provider revision associated with the visible browse or search result.</summary>
    public string? ConsistencyToken { get; }
}

/// <summary>State-change notification from a browser session.</summary>
public sealed class FileBrowserSnapshotChangedEventArgs(FileBrowserSnapshot snapshot) : EventArgs
{
    public FileBrowserSnapshot Snapshot { get; } = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
}

/// <summary>Renderer-facing browsing, navigation, search, and selection facade.</summary>
public interface IFileBrowserSession : IAsyncDisposable
{
    event EventHandler<FileBrowserSnapshotChangedEventArgs>? Changed;

    FileBrowserSnapshot Snapshot { get; }

    ValueTask InitializeAsync(FileBrowserSourceId? sourceId = null, FileBrowserItemKey? startAt = null, CancellationToken cancellationToken = default);

    ValueTask ChangeSourceAsync(FileBrowserSourceId sourceId, CancellationToken cancellationToken = default);

    ValueTask NavigateAsync(FileBrowserItemKey containerKey, CancellationToken cancellationToken = default);

    ValueTask GoBackAsync(CancellationToken cancellationToken = default);

    ValueTask GoForwardAsync(CancellationToken cancellationToken = default);

    ValueTask GoUpAsync(CancellationToken cancellationToken = default);

    ValueTask LoadMoreAsync(CancellationToken cancellationToken = default);

    ValueTask RefreshAsync(CancellationToken cancellationToken = default);

    ValueTask RetryAsync(CancellationToken cancellationToken = default);

    ValueTask SetSortAsync(FileBrowserSortDescriptor sort, CancellationToken cancellationToken = default);

    ValueTask SetFilterAsync(FileBrowserFilter filter, CancellationToken cancellationToken = default);

    ValueTask SetIncludeDescendantsAsync(bool includeDescendants, CancellationToken cancellationToken = default);

    ValueTask SearchAsync(string query, FileBrowserSearchScope scope, CancellationToken cancellationToken = default);

    ValueTask ClearSearchAsync(CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default);

    ValueTask<FileBrowserActionResult> ExecuteActionAsync(
        FileBrowserActionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<FileBrowserContentLease> OpenReadAsync(
        FileBrowserReadRequest request,
        CancellationToken cancellationToken = default);

    void Select(FileBrowserItemKey itemKey, bool toggle = false);

    void ClearSelection();

    ValueTask InvalidateItemAsync(
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default);

    ValueTask InvalidateSourceAsync(
        FileBrowserSourceId sourceId,
        CancellationToken cancellationToken = default);

    ValueTask InvalidateAllAsync(CancellationToken cancellationToken = default);

    ValueTask UpdateSourcesAsync(
        FileBrowserSourceSet sources,
        CancellationToken cancellationToken = default);
}

