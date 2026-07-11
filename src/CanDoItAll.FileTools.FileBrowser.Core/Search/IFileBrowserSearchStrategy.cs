namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Provides loaded-tree access and bounded browse calls to a search strategy.</summary>
public interface IFileBrowserSearchData : IFileBrowserLoadedTree
{
    IReadOnlyList<FileBrowserItem> CurrentItems { get; }

    ValueTask<FileBrowserPage> BrowseAndCacheAsync(
        FileBrowserBrowseRequest request,
        FileBrowserPageApplyMode applyMode,
        CancellationToken cancellationToken = default);
}

/// <summary>Context shared by interchangeable search algorithms.</summary>
public sealed record FileBrowserSearchStrategyContext(
    IFileBrowserProvider Provider,
    IFileBrowserSearchData Data,
    FileBrowserSearchRequest Request);

/// <summary>Interchangeable algorithm for one explicit search scope.</summary>
public interface IFileBrowserSearchStrategy
{
    string Id { get; }

    FileBrowserSearchScope Scope { get; }

    bool CanSearch(IFileBrowserProvider provider);

    ValueTask<FileBrowserSearchPage> SearchAsync(
        FileBrowserSearchStrategyContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Validated search strategy catalog used by browser sessions.</summary>
public sealed class FileBrowserSearchStrategyCatalog
{
    private readonly IReadOnlyDictionary<FileBrowserSearchScope, IFileBrowserSearchStrategy> strategies;

    public FileBrowserSearchStrategyCatalog(IEnumerable<IFileBrowserSearchStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        var values = strategies.ToArray();
        var duplicate = values.GroupBy(strategy => strategy.Scope).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"More than one strategy was registered for {duplicate.Key}.", nameof(strategies));
        }

        this.strategies = values.ToDictionary(strategy => strategy.Scope);
    }

    public static FileBrowserSearchStrategyCatalog CreateDefault()
        => new(
        [
            new LoadedFolderFileBrowserSearchStrategy(),
            new LoadedDescendantsFileBrowserSearchStrategy(),
            new ProviderFileBrowserSearchStrategy(),
            new ProgressiveFileBrowserSearchStrategy()
        ]);

    public IFileBrowserSearchStrategy Get(FileBrowserSearchScope scope, IFileBrowserProvider provider)
    {
        if (!strategies.TryGetValue(scope, out var strategy) || !strategy.CanSearch(provider))
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.Unsupported,
                $"Search scope '{scope}' is not supported by this source."));
        }

        return strategy;
    }

    public IReadOnlyList<FileBrowserSearchScope> GetAvailable(IFileBrowserProvider provider)
        => strategies.Values
            .Where(strategy => strategy.CanSearch(provider))
            .Select(strategy => strategy.Scope)
            .Order()
            .ToArray();
}

