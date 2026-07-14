using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileBrowser.Components;

/// <summary>Responsive, provider-neutral renderer over an <see cref="IFileBrowserSession"/>.</summary>
public partial class FileBrowser : ComponentBase, IAsyncDisposable
{
    private readonly FileBrowserInteractionDispatcher interactionDispatcher = new();
    private readonly FileBrowserSearchDebouncer searchDebouncer = new();
    private readonly CancellationTokenSource lifetime = new();
    private IFileBrowserSession? boundSession;
    private IFileBrowserHostActionCatalog? previousHostActionCatalog;
    private CancellationTokenSource? bindingLifetime;
    private FileBrowserSnapshot snapshot = default!;
    private FileBrowserViewMode viewMode;
    private FileBrowserSearchScope searchScope = FileBrowserSearchScope.LoadedFolder;
    private string searchText = string.Empty;
    private bool viewModeInitialized;
    private bool initializationPending;
    private bool disposed;

    [Parameter, EditorRequired]
    public IFileBrowserSession Session { get; set; } = default!;

    [Parameter]
    public string AriaLabel { get; set; } = "File browser";

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public FileBrowserDisplayMode DisplayMode { get; set; } = FileBrowserDisplayMode.Standard;

    [Parameter]
    public string IncludeDescendantsLabel { get; set; } = "Include descendants";

    [Parameter]
    public bool InitializeOnFirstRender { get; set; } = true;

    [Parameter]
    public FileBrowserSourceId? InitialSourceId { get; set; }

    [Parameter]
    public FileBrowserItemKey? InitialItemKey { get; set; }

    [Parameter]
    public FileBrowserViewMode InitialViewMode { get; set; } = FileBrowserViewMode.List;

    [Parameter]
    public int SearchDebounceMilliseconds { get; set; } = 280;

    [Parameter]
    public EventCallback<FileBrowserSnapshot> SnapshotChanged { get; set; }

    [Parameter]
    public EventCallback<FileBrowserViewMode> ViewModeChanged { get; set; }

    [Parameter]
    public EventCallback<FileBrowserItemInvokedEventArgs> ItemInvoked { get; set; }

    [Parameter]
    public EventCallback<FileBrowserItemActionEventArgs> ActionRequested { get; set; }

    [Parameter]
    public IFileBrowserHostActionCatalog? HostActionCatalog { get; set; }

    public FileBrowserSnapshot Snapshot => snapshot;

    private string RootClass
        => string.IsNullOrWhiteSpace(Class)
            ? "ft-file-browser"
            : $"ft-file-browser {Class.Trim()}";

    protected override void OnParametersSet()
    {
        ValidateParameters();
        if (!viewModeInitialized)
        {
            viewMode = InitialViewMode;
            viewModeInitialized = true;
        }

        bool hostActionCatalogChanged = !ReferenceEquals(previousHostActionCatalog, HostActionCatalog);
        previousHostActionCatalog = HostActionCatalog;
        if (!ReferenceEquals(boundSession, Session))
        {
            interactionDispatcher.ChangeSession();
            searchDebouncer.Cancel();
            bindingLifetime?.Cancel();
            bindingLifetime?.Dispose();
            if (boundSession is not null)
            {
                boundSession.Changed -= HandleSessionChanged;
            }

            boundSession = Session;
            bindingLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            boundSession.Changed += HandleSessionChanged;
            snapshot = boundSession.Snapshot;
            SynchronizeSearchFromSnapshot(force: true);
            initializationPending = ShouldInitialize(snapshot);
        }
        else if (hostActionCatalogChanged)
        {
            interactionDispatcher.AcceptSnapshot();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!initializationPending || boundSession is null || !ShouldInitialize(snapshot))
        {
            initializationPending = false;
            return;
        }

        initializationPending = false;
        IFileBrowserSession initializingSession = boundSession;
        long initializingSessionVersion = interactionDispatcher.Capture().SessionVersion;
        CancellationToken token = BindingToken;
        try
        {
            await initializingSession.InitializeAsync(InitialSourceId, InitialItemKey, token);
        }
        catch (OperationCanceledException) when (
            disposed || !interactionDispatcher.IsCurrentSession(initializingSessionVersion))
        {
            // Session replacement or component disposal superseded initialization.
        }
    }

    private void ValidateParameters()
    {
        if (Session is null)
        {
            throw new InvalidOperationException("FileBrowser requires a session.");
        }

        if (!Enum.IsDefined(DisplayMode))
        {
            throw new InvalidOperationException("DisplayMode is not valid.");
        }

        if (!Enum.IsDefined(InitialViewMode))
        {
            throw new InvalidOperationException("InitialViewMode is not valid.");
        }

        if (SearchDebounceMilliseconds is < 0 or > 60_000)
        {
            throw new InvalidOperationException("SearchDebounceMilliseconds must be between 0 and 60000.");
        }

        if (string.IsNullOrWhiteSpace(AriaLabel) || string.IsNullOrWhiteSpace(IncludeDescendantsLabel))
        {
            throw new InvalidOperationException("Browser labels cannot be empty.");
        }
    }

    private async void HandleSessionChanged(object? sender, FileBrowserSnapshotChangedEventArgs args)
    {
        try
        {
            await InvokeAsync(async () =>
            {
                if (disposed || !ReferenceEquals(sender, boundSession))
                {
                    return;
                }

                if (args.Snapshot.Revision <= snapshot.Revision)
                {
                    return;
                }

                interactionDispatcher.AcceptSnapshot();
                snapshot = args.Snapshot;
                SynchronizeSearchFromSnapshot(force: false);
                StateHasChanged();
                await SnapshotChanged.InvokeAsync(snapshot);
            });
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    private void SynchronizeSearchFromSnapshot(bool force)
    {
        if (force || !searchDebouncer.HasPending)
        {
            searchText = snapshot.Search?.Query ?? string.Empty;
        }

        if (!snapshot.AvailableSearchScopes.Contains(searchScope))
        {
            searchScope = snapshot.AvailableSearchScopes.Count > 0
                ? snapshot.AvailableSearchScopes[0]
                : FileBrowserSearchScope.LoadedFolder;
        }
    }

    private Task InitializeAsync()
        => BoundSession.InitializeAsync(InitialSourceId, InitialItemKey, BindingToken).AsTask();

    private async Task ChangeSearchTextAsync(string value)
    {
        searchText = value;
        if (string.IsNullOrWhiteSpace(value))
        {
            searchDebouncer.Cancel();
            await BoundSession.ClearSearchAsync(BindingToken);
            return;
        }

        string query = value;
        FileBrowserSearchScope scope = searchScope;
        IFileBrowserSession searchSession = BoundSession;
        long searchSessionVersion = interactionDispatcher.Capture().SessionVersion;
        await searchDebouncer.ScheduleAsync(
            TimeSpan.FromMilliseconds(SearchDebounceMilliseconds),
            token => interactionDispatcher.IsCurrentSession(searchSessionVersion)
                && ReferenceEquals(searchSession, boundSession)
                ? searchSession.SearchAsync(query, scope, token)
                : ValueTask.CompletedTask,
            BindingToken);
    }

    private async Task ChangeSearchScopeAsync(FileBrowserSearchScope value)
    {
        searchDebouncer.Cancel();
        searchScope = value;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            await BoundSession.SearchAsync(searchText, searchScope, BindingToken);
        }
    }

    private async Task ChangeViewModeAsync(FileBrowserViewMode value)
    {
        viewMode = value;
        await ViewModeChanged.InvokeAsync(value);
    }

    private async Task ChangeSourceAsync(FileBrowserSourceId sourceId)
    {
        searchDebouncer.Cancel();
        searchText = string.Empty;
        await BoundSession.ChangeSourceAsync(sourceId, BindingToken);
    }

    private Task NavigateAsync(FileBrowserItemKey key)
        => BoundSession.NavigateAsync(key, BindingToken).AsTask();

    private Task GoBackAsync() => BoundSession.GoBackAsync(BindingToken).AsTask();

    private Task GoForwardAsync() => BoundSession.GoForwardAsync(BindingToken).AsTask();

    private Task GoUpAsync() => BoundSession.GoUpAsync(BindingToken).AsTask();

    private Task RefreshAsync() => BoundSession.RefreshAsync(BindingToken).AsTask();

    private Task RetryAsync() => BoundSession.RetryAsync(BindingToken).AsTask();

    private Task LoadMoreAsync() => BoundSession.LoadMoreAsync(BindingToken).AsTask();

    private Task ChangeIncludeDescendantsAsync(bool value)
        => BoundSession.SetIncludeDescendantsAsync(value, BindingToken).AsTask();

    private Task ChangeCategoryAsync(FileBrowserItemCategory? category)
    {
        FileBrowserFilter current = snapshot.Filter;
        var filter = new FileBrowserFilter(
            current.Kinds,
            category.HasValue ? [category.Value] : [],
            current.Extensions,
            current.MediaTypePrefix);
        return BoundSession.SetFilterAsync(filter, BindingToken).AsTask();
    }

    private Task ChangeSortAsync(FileBrowserSortField sortField)
    {
        FileBrowserSortDirection direction = snapshot.Sort.Field == sortField
            ? Reverse(snapshot.Sort.Direction)
            : FileBrowserSortDirection.Ascending;
        return SetSortAsync(sortField, direction);
    }

    private Task ReverseSortAsync()
        => SetSortAsync(snapshot.Sort.Field, Reverse(snapshot.Sort.Direction));

    private Task SetSortAsync(FileBrowserSortField sortField, FileBrowserSortDirection direction)
        => BoundSession.SetSortAsync(
            new FileBrowserSortDescriptor(sortField, direction, snapshot.Sort.FoldersFirst),
            BindingToken).AsTask();

    private EventCallback<FileBrowserItem> CreateSelectCallback(bool toggle)
        => interactionDispatcher.CreateSelectCallback(this, BoundSession, snapshot, toggle);

    private EventCallback<FileBrowserItemInvokedEventArgs> CreateActivateCallback()
        => interactionDispatcher.CreateActivateCallback(
            this,
            BoundSession,
            snapshot,
            BindingToken,
            ItemInvoked);

    private EventCallback<FileBrowserItemActionEventArgs> CreateActionCallback()
        => interactionDispatcher.CreateActionCallback(
            this,
            BoundSession,
            snapshot,
            ActionRequested);

    private IFileBrowserSession BoundSession
        => boundSession ?? throw new InvalidOperationException("The browser session is not bound.");

    private CancellationToken BindingToken
        => bindingLifetime?.Token
            ?? throw new InvalidOperationException("The browser session lifetime is not bound.");

    private bool ShouldInitialize(FileBrowserSnapshot candidate)
        => InitializeOnFirstRender
            && candidate.Sources.Count > 0
            && candidate.CurrentSource is null
            && !candidate.IsBusy;

    private bool HasActiveLocation
        => snapshot.CurrentSource is not null && snapshot.Location is not null;

    private bool HasSourceNavigation
        => snapshot.Sources.Count > 1 || snapshot.CurrentSource is not null;

    private static FileBrowserSortDirection Reverse(FileBrowserSortDirection direction)
        => direction == FileBrowserSortDirection.Ascending
            ? FileBrowserSortDirection.Descending
            : FileBrowserSortDirection.Ascending;

    private static string FormatOperation(FileBrowserOperationKind operation)
        => operation switch
        {
            FileBrowserOperationKind.LoadingFolder => "Loading folder\u2026",
            FileBrowserOperationKind.LoadingMore => "Loading more items\u2026",
            FileBrowserOperationKind.Refreshing => "Refreshing\u2026",
            FileBrowserOperationKind.Searching => "Searching\u2026",
            _ => "Loading\u2026"
        };

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        interactionDispatcher.ChangeSession();
        if (boundSession is not null)
        {
            boundSession.Changed -= HandleSessionChanged;
        }

        if (bindingLifetime is not null)
        {
            await bindingLifetime.CancelAsync();
        }

        await lifetime.CancelAsync();
        await searchDebouncer.DisposeAsync();
        bindingLifetime?.Dispose();
        lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
