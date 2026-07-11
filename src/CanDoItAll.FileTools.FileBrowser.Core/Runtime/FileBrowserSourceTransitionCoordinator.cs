namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>
/// Resolves and loads an updated source set without mutating the active browser workspace. The
/// runtime can therefore commit the complete transition or retain its previous state.
/// </summary>
internal sealed class FileBrowserSourceTransitionCoordinator
{
    private readonly FileBrowserSessionOptions options;
    private readonly FileBrowserNavigator resolver = new();

    public FileBrowserSourceTransitionCoordinator(FileBrowserSessionOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async ValueTask<FileBrowserStagedSourceTransition> StageAsync(
        FileBrowserSourceSet updatedSources,
        FileBrowserSourceId? currentSource,
        FileBrowserItemKey? currentLocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(updatedSources);
        if (updatedSources.Sources.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new FileBrowserStagedSourceTransition(updatedSources, null, null);
        }

        FileBrowserSourceId selected = currentSource.HasValue
            && updatedSources.TryGet(currentSource.Value, out _)
                ? currentSource.Value
                : updatedSources.Sources[0].Id;
        FileBrowserItemKey? preservedLocation = selected == currentSource ? currentLocation : null;
        try
        {
            return await StageLocationAsync(
                updatedSources,
                selected,
                preservedLocation,
                cancellationToken).ConfigureAwait(false);
        }
        catch (FileBrowserProviderException) when (preservedLocation.HasValue)
        {
            return await StageLocationAsync(
                updatedSources,
                selected,
                null,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<FileBrowserStagedSourceTransition> StageLocationAsync(
        FileBrowserSourceSet updatedSources,
        FileBrowserSourceId sourceId,
        FileBrowserItemKey? startAt,
        CancellationToken cancellationToken)
    {
        FileBrowserNavigationTarget target = await resolver.ResolveInitialAsync(
            updatedSources,
            sourceId,
            startAt,
            options.Metadata,
            cancellationToken).ConfigureAwait(false);
        var stagingLoader = new FileBrowserLoader(new DisabledFileBrowserStateStore(), options);
        FileBrowserLoadedContainer container = await stagingLoader.LoadAsync(
            target.Provider,
            target.Location,
            options.DefaultSort,
            FileBrowserFilter.None,
            includeDescendants: false,
            force: true,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new FileBrowserStagedSourceTransition(updatedSources, target, container);
    }
}

internal sealed record FileBrowserStagedSourceTransition(
    FileBrowserSourceSet Sources,
    FileBrowserNavigationTarget? Target,
    FileBrowserLoadedContainer? Container);
