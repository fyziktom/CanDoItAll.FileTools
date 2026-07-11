namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Coordinates loaded-item resolution, action dispatch, content, and folder activation.</summary>
internal sealed class FileBrowserSessionActionCoordinator
{
    private readonly FileBrowserLoader loader;
    private readonly FileBrowserActionDispatcher dispatcher;
    private readonly FileBrowserBrowseWorkspace workspace;
    private readonly Func<FileBrowserItemKey, CancellationToken, ValueTask> navigate;
    private readonly Func<FileBrowserItemKey?> currentLocation;
    private readonly Func<IReadOnlyList<FileBrowserItem>> currentItems;

    public FileBrowserSessionActionCoordinator(
        FileBrowserLoader loader,
        FileBrowserActionDispatcher dispatcher,
        FileBrowserBrowseWorkspace workspace,
        Func<FileBrowserItemKey, CancellationToken, ValueTask> navigate,
        Func<FileBrowserItemKey?> currentLocation,
        Func<IReadOnlyList<FileBrowserItem>> currentItems)
    {
        this.loader = loader;
        this.dispatcher = dispatcher;
        this.workspace = workspace;
        this.navigate = navigate;
        this.currentLocation = currentLocation;
        this.currentItems = currentItems;
    }

    public ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken)
    {
        (IFileBrowserProvider provider, FileBrowserItem item) = ResolveLoadedItem(itemKey);
        return dispatcher.GetActionsAsync(provider, item, cancellationToken);
    }

    public async ValueTask<FileBrowserExecutedAction> ExecuteActionAsync(
        FileBrowserActionRequest request,
        CancellationToken cancellationToken)
    {
        (IFileBrowserProvider provider, FileBrowserItem item) = ResolveLoadedItem(request.ItemKey);
        FileBrowserActionDispatch dispatch = await dispatcher.DispatchAsync(
            provider,
            item,
            request,
            cancellationToken);
        if (dispatch.NavigationKey is not { } navigationKey)
        {
            return new FileBrowserExecutedAction(dispatch.Result!, NavigationCommitted: false);
        }

        await navigate(navigationKey, cancellationToken);
        FileBrowserActionResult result = currentLocation() == navigationKey
            ? FileBrowserActionResult.Success()
            : FileBrowserActionResult.Failure(new FileBrowserError(
                FileBrowserErrorCode.InvalidOperation,
                "The folder could not be opened."));
        return new FileBrowserExecutedAction(result, result.Succeeded);
    }

    public ValueTask<FileBrowserContentLease> OpenReadAsync(
        FileBrowserReadRequest request,
        CancellationToken cancellationToken)
    {
        (IFileBrowserProvider provider, _) = ResolveLoadedItem(request.ItemKey);
        return dispatcher.OpenReadAsync(provider, request, cancellationToken);
    }

    private (IFileBrowserProvider Provider, FileBrowserItem Item) ResolveLoadedItem(FileBrowserItemKey itemKey)
    {
        IFileBrowserProvider provider = workspace.Provider
            ?? throw new InvalidOperationException("The file browser session is not initialized.");
        if (itemKey.SourceId != provider.Descriptor.Id)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.InvalidLocation,
                "The item does not belong to the active file browser source."));
        }

        return (provider, loader.ResolveLoadedItem(itemKey, currentItems()));
    }
}
