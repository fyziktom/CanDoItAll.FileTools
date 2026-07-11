namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

internal sealed class RenderedFileBrowserTestSession(FileBrowserSnapshot snapshot) : IFileBrowserSession
{
    public event EventHandler<FileBrowserSnapshotChangedEventArgs>? Changed;

    public FileBrowserSnapshot Snapshot { get; private set; } = snapshot;

    public List<(FileBrowserItemKey Key, bool Toggle)> Selections { get; } = [];

    public List<FileBrowserItemKey> Navigations { get; } = [];

    public List<FileBrowserSourceId> SourceChanges { get; } = [];

    public int ExecuteActionCalls { get; private set; }

    public Func<FileBrowserItemKey, CancellationToken, ValueTask<IReadOnlyList<FileBrowserActionDescriptor>>>?
        GetActionsHandler
    { get; set; }

    public void Publish(FileBrowserSnapshot next)
    {
        Snapshot = next;
        Changed?.Invoke(this, new FileBrowserSnapshotChangedEventArgs(next));
    }

    public ValueTask InitializeAsync(
        FileBrowserSourceId? sourceId = null,
        FileBrowserItemKey? startAt = null,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask ChangeSourceAsync(
        FileBrowserSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        SourceChanges.Add(sourceId);
        return ValueTask.CompletedTask;
    }

    public ValueTask NavigateAsync(
        FileBrowserItemKey containerKey,
        CancellationToken cancellationToken = default)
    {
        Navigations.Add(containerKey);
        return ValueTask.CompletedTask;
    }

    public ValueTask GoBackAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask GoForwardAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask GoUpAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask LoadMoreAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask RetryAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask SetSortAsync(
        FileBrowserSortDescriptor sort,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask SetFilterAsync(
        FileBrowserFilter filter,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask SetIncludeDescendantsAsync(
        bool includeDescendants,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask SearchAsync(
        string query,
        FileBrowserSearchScope scope,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask ClearSearchAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
        => GetActionsHandler?.Invoke(itemKey, cancellationToken)
            ?? ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>([]);

    public ValueTask<FileBrowserActionResult> ExecuteActionAsync(
        FileBrowserActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ExecuteActionCalls++;
        return ValueTask.FromResult(FileBrowserActionResult.Success());
    }

    public ValueTask<FileBrowserContentLease> OpenReadAsync(
        FileBrowserReadRequest request,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new FileBrowserContentLease(new MemoryStream()));

    public void Select(FileBrowserItemKey itemKey, bool toggle = false)
        => Selections.Add((itemKey, toggle));

    public void ClearSelection()
    {
    }

    public ValueTask InvalidateItemAsync(
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask InvalidateSourceAsync(
        FileBrowserSourceId sourceId,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask InvalidateAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask UpdateSourcesAsync(
        FileBrowserSourceSet sources,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal static class RenderedFileBrowserSnapshotFactory
{
    public static FileBrowserSnapshot Create(
        IReadOnlyList<FileBrowserItem>? items = null,
        long revision = 1,
        bool hasSource = true,
        FileBrowserOperationKind operation = FileBrowserOperationKind.Idle,
        IReadOnlyList<FileBrowserPageWarning>? warnings = null,
        FileBrowserSearchSnapshot? search = null,
        IReadOnlyList<FileBrowserSourceDescriptor>? sources = null,
        FileBrowserSourceDescriptor? currentSource = null)
    {
        if (!hasSource)
        {
            return new FileBrowserSnapshot(
                [],
                null,
                null,
                [],
                new HashSet<FileBrowserItemKey>(),
                new FileBrowserSortDescriptor(),
                FileBrowserFilter.None,
                includeDescendants: false,
                [],
                null,
                operation,
                null,
                [],
                null,
                0,
                canGoBack: false,
                canGoForward: false,
                canGoUp: false,
                FileBrowserTreeDiagnostics.Empty,
                revision);
        }

        FileBrowserSourceDescriptor descriptor = currentSource ?? CreateSource();
        var root = new FileBrowserItem(
            new FileBrowserItemKey(descriptor.Id, "/"),
            null,
            "Root",
            FileBrowserItemKind.Container,
            FileBrowserItemCategory.Folder,
            capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);
        IReadOnlyList<FileBrowserItem> visibleItems = items ?? [];
        return new FileBrowserSnapshot(
            sources ?? [descriptor],
            descriptor,
            new FileBrowserLocation([root]),
            visibleItems,
            new HashSet<FileBrowserItemKey>(),
            new FileBrowserSortDescriptor(),
            FileBrowserFilter.None,
            includeDescendants: false,
            descriptor.SupportedSearchScopes.OrderBy(scope => scope).ToArray(),
            search,
            operation,
            null,
            warnings ?? [],
            null,
            visibleItems.Count,
            canGoBack: false,
            canGoForward: false,
            canGoUp: false,
            FileBrowserTreeDiagnostics.Empty,
            revision,
            FileBrowserCompleteness.Complete,
            $"revision-{revision}");
    }

    public static FileBrowserSourceDescriptor CreateSource(
        string id = "test",
        string displayName = "Test files")
        => new(
            new FileBrowserSourceId(id),
            displayName,
            capabilities: FileBrowserSourceCapabilities.PagedBrowse
                | FileBrowserSourceCapabilities.RecursiveBrowse
                | FileBrowserSourceCapabilities.CustomActions);

    public static FileBrowserItem Recreate(
        FileBrowserItem template,
        string name,
        FileBrowserItemCapabilities? capabilities = null)
        => new(
            template.Key,
            template.ParentKey,
            name,
            template.Kind,
            template.Category,
            template.DisplayPath,
            template.ChildState,
            template.ChildCount,
            template.Size,
            template.MediaType,
            template.Owner,
            template.CreatedAt,
            template.ModifiedAt,
            template.ContentIdentity,
            template.MetadataState,
            capabilities ?? template.Capabilities,
            template.OpenUri,
            template.DownloadUri,
            template.Metadata);
}
