namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Filters only the active materialized folder page.</summary>
public sealed class LoadedFolderFileBrowserSearchStrategy : IFileBrowserSearchStrategy
{
    public string Id => "loaded-folder";

    public FileBrowserSearchScope Scope => FileBrowserSearchScope.LoadedFolder;

    public bool CanSearch(IFileBrowserProvider provider)
        => provider.Descriptor.SupportedSearchScopes.Contains(Scope);

    public ValueTask<FileBrowserSearchPage> SearchAsync(
        FileBrowserSearchStrategyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = FileBrowserSearchMatching.FilterAndOrder(context.Data.CurrentItems, context.Request);
        return ValueTask.FromResult(FileBrowserSearchMatching.Page(
            matches,
            context.Request,
            Id,
            isPartial: false,
            scannedItems: context.Data.CurrentItems.Count));
    }
}

/// <summary>Searches only descendants already retained in the browser tree store.</summary>
public sealed class LoadedDescendantsFileBrowserSearchStrategy : IFileBrowserSearchStrategy
{
    public string Id => "loaded-descendants";

    public FileBrowserSearchScope Scope => FileBrowserSearchScope.LoadedDescendants;

    public bool CanSearch(IFileBrowserProvider provider)
        => provider.Descriptor.SupportedSearchScopes.Contains(Scope);

    public ValueTask<FileBrowserSearchPage> SearchAsync(
        FileBrowserSearchStrategyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = context.Data.GetLoadedDescendants(context.Request.ContainerKey);
        var matches = FileBrowserSearchMatching.FilterAndOrder(candidates, context.Request);
        return ValueTask.FromResult(FileBrowserSearchMatching.Page(
            matches,
            context.Request,
            Id,
            isPartial: true,
            scannedItems: candidates.Count,
            warnings:
            [
                new FileBrowserPageWarning(
                    "loaded-scope",
                    "Results include only descendants already loaded in this browser session.")
            ]));
    }
}

