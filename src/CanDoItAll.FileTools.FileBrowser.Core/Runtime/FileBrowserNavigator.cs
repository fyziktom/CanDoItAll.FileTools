namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Owns path resolution/validation and navigation history.</summary>
public sealed class FileBrowserNavigator
{
    private readonly FileBrowserNavigationState state = new();

    public FileBrowserLocation? Current => state.Current;

    public bool CanGoBack => state.CanGoBack;

    public bool CanGoForward => state.CanGoForward;

    public bool CanGoUp => state.CanGoUp;

    public async ValueTask<FileBrowserNavigationTarget> ResolveInitialAsync(
        FileBrowserSourceSet sources,
        FileBrowserSourceId sourceId,
        FileBrowserItemKey? startAt,
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        IFileBrowserProvider provider = sources.Get(sourceId);
        IReadOnlyList<FileBrowserItem> path;
        if (startAt.HasValue)
        {
            path = await provider.GetPathAsync(startAt.Value, metadata, cancellationToken);
        }
        else
        {
            FileBrowserItem root = await provider.GetRootAsync(metadata, cancellationToken);
            path = [root];
        }

        cancellationToken.ThrowIfCancellationRequested();
        ValidatePath(provider, path, startAt);
        return new FileBrowserNavigationTarget(provider, new FileBrowserLocation(path));
    }

    public async ValueTask<FileBrowserNavigationTarget> ResolveAsync(
        FileBrowserSourceSet sources,
        FileBrowserItemKey containerKey,
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        IFileBrowserProvider provider = sources.Get(containerKey.SourceId);
        IReadOnlyList<FileBrowserItem> path = await provider.GetPathAsync(
            containerKey,
            metadata,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePath(provider, path, containerKey);
        return new FileBrowserNavigationTarget(provider, new FileBrowserLocation(path));
    }

    public FileBrowserLocation PeekBack() => state.PeekBack();

    public FileBrowserLocation PeekForward() => state.PeekForward();

    public FileBrowserLocation PeekUp() => state.PeekUp();

    public void Reset(FileBrowserLocation location) => state.Reset(location);

    public void Clear() => state.Clear();

    public void Navigate(FileBrowserLocation location) => state.Navigate(location);

    public void CommitBack() => state.GoBack();

    public void CommitForward() => state.GoForward();

    public void CommitUp() => state.GoUp();

    internal FileBrowserNavigationCheckpoint Capture() => state.Capture();

    internal void Restore(FileBrowserNavigationCheckpoint checkpoint) => state.Restore(checkpoint);

    private static void ValidatePath(
        IFileBrowserProvider provider,
        IReadOnlyList<FileBrowserItem> path,
        FileBrowserItemKey? requestedKey)
    {
        if (path is null || path.Count == 0)
        {
            throw InvalidProviderPath();
        }

        var visited = new HashSet<FileBrowserItemKey>();
        for (var index = 0; index < path.Count; index++)
        {
            FileBrowserItem item = path[index];
            FileBrowserItemKey? expectedParent = index == 0 ? null : path[index - 1].Key;
            if (item.Key.SourceId != provider.Descriptor.Id
                || !item.IsContainer
                || item.ParentKey != expectedParent
                || !visited.Add(item.Key))
            {
                throw InvalidProviderPath();
            }
        }

        if (requestedKey.HasValue && path[^1].Key != requestedKey.Value)
        {
            throw InvalidProviderPath();
        }
    }

    private static FileBrowserProviderException InvalidProviderPath()
        => new(new FileBrowserError(
            FileBrowserErrorCode.CorruptProviderResponse,
            "The provider returned an invalid browser path."));
}

/// <summary>A validated provider/location pair prepared for a navigation commit.</summary>
public sealed record FileBrowserNavigationTarget(
    IFileBrowserProvider Provider,
    FileBrowserLocation Location);
