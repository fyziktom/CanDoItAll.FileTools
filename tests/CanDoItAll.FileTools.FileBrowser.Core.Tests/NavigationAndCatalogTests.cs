namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class NavigationAndCatalogTests
{
    [Fact]
    public void LocationRequiresNonEmptySingleSourceContainerPath()
    {
        var root = TestFileBrowserFactory.Container("root", childState: FileBrowserChildState.HasChildren);

        Assert.Throws<ArgumentException>(() => new FileBrowserLocation([]));
        Assert.Throws<ArgumentException>(() => new FileBrowserLocation(
            [root, TestFileBrowserFactory.File("file", root.Key)]));
        Assert.Throws<ArgumentException>(() => new FileBrowserLocation(
            [root, TestFileBrowserFactory.Container("foreign", source: "other")]));

        var location = new FileBrowserLocation([root]);
        Assert.Same(root, location.Current);
        Assert.Equal(root.Key, location.Key);
        Assert.False(location.CanGoUp);
        Assert.Same(location, location.Parent());
    }

    [Fact]
    public void NavigationTracksBackForwardUpAndClearsForwardOnNewBranch()
    {
        var root = TestFileBrowserFactory.Container("root");
        var alpha = TestFileBrowserFactory.Container("alpha", root.Key);
        var beta = TestFileBrowserFactory.Container("beta", root.Key);
        var rootLocation = new FileBrowserLocation([root]);
        var alphaLocation = new FileBrowserLocation([root, alpha]);
        var betaLocation = new FileBrowserLocation([root, beta]);
        var navigation = new FileBrowserNavigationState();
        navigation.Reset(rootLocation);

        navigation.Navigate(alphaLocation);
        Assert.Same(alphaLocation, navigation.Current);
        Assert.True(navigation.CanGoBack);
        Assert.False(navigation.CanGoForward);
        Assert.True(navigation.CanGoUp);

        Assert.Same(rootLocation, navigation.GoBack());
        Assert.False(navigation.CanGoBack);
        Assert.True(navigation.CanGoForward);

        Assert.Same(alphaLocation, navigation.GoForward());
        Assert.True(navigation.CanGoBack);
        Assert.False(navigation.CanGoForward);

        Assert.Equal(root.Key, navigation.GoUp().Key);
        Assert.True(navigation.CanGoBack);
        Assert.False(navigation.CanGoForward);
        Assert.Same(alphaLocation, navigation.GoBack());
        Assert.True(navigation.CanGoForward);

        navigation.Navigate(betaLocation);
        Assert.Same(betaLocation, navigation.Current);
        Assert.True(navigation.CanGoBack);
        Assert.False(navigation.CanGoForward);
    }

    [Fact]
    public void SameKeyNavigationAndReplacementRefreshPathWithoutAddingHistory()
    {
        var root = TestFileBrowserFactory.Container("root");
        var original = TestFileBrowserFactory.Container("folder", root.Key, name: "Old name");
        var refreshed = TestFileBrowserFactory.Container("folder", root.Key, name: "Current name");
        var originalLocation = new FileBrowserLocation([root, original]);
        var refreshedLocation = new FileBrowserLocation([root, refreshed]);
        var navigation = new FileBrowserNavigationState();

        navigation.Navigate(originalLocation);
        navigation.Navigate(refreshedLocation);

        Assert.Equal("Current name", navigation.Current!.Current.Name);
        Assert.False(navigation.CanGoBack);
        Assert.False(navigation.CanGoForward);

        var replacement = new FileBrowserLocation([
            TestFileBrowserFactory.Container("root", name: "Refreshed root"),
            refreshed
        ]);
        navigation.ReplaceCurrent(replacement);
        Assert.Equal("Refreshed root", navigation.Current.Path[0].Name);
        Assert.Throws<InvalidOperationException>(() => navigation.ReplaceCurrent(
            new FileBrowserLocation([root])));
    }

    [Fact]
    public void InvalidNavigationTransitionsThrowWithoutChangingState()
    {
        var root = new FileBrowserLocation([TestFileBrowserFactory.Container("root")]);
        var navigation = new FileBrowserNavigationState();

        Assert.Throws<InvalidOperationException>(() => navigation.GoBack());
        Assert.Throws<InvalidOperationException>(() => navigation.GoForward());
        Assert.Throws<InvalidOperationException>(() => navigation.GoUp());

        navigation.Reset(root);
        Assert.Throws<InvalidOperationException>(() => navigation.GoBack());
        Assert.Throws<InvalidOperationException>(() => navigation.GoForward());
        Assert.Throws<InvalidOperationException>(() => navigation.GoUp());
        Assert.Same(root, navigation.Current);
    }

    [Fact]
    public void ProviderCatalogRejectsEmptyAndDuplicateSourceRegistrations()
    {
        Assert.Throws<ArgumentException>(() => new FileBrowserProviderCatalog([]));

        var first = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("same", "First"));
        var second = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("same", "Second"));
        var duplicate = Assert.Throws<ArgumentException>(() => new FileBrowserProviderCatalog([first, second]));

        Assert.Contains("same", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderCatalogOrdersSourcesAndHandlesKnownAndUnknownLookups()
    {
        var zulu = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("z", "Zulu"));
        var alphaSecond = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("c", "alpha"));
        var alphaFirst = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("a", "Alpha"));
        var catalog = new FileBrowserProviderCatalog([zulu, alphaSecond, alphaFirst]);

        Assert.Equal(["a", "c", "z"], catalog.Sources.Select(source => source.Id.Value));
        Assert.Same(alphaFirst, catalog.Get(TestFileBrowserFactory.Source("a")));
        Assert.True(catalog.TryGet(TestFileBrowserFactory.Source("z"), out var found));
        Assert.Same(zulu, found);
        Assert.False(catalog.TryGet(TestFileBrowserFactory.Source("missing"), out var missing));
        Assert.Null(missing);
        Assert.Throws<KeyNotFoundException>(() => catalog.Get(TestFileBrowserFactory.Source("missing")));
    }

    [Fact]
    public void SearchStrategyCatalogRejectsDuplicateScopesAndUnknownOrUnavailableScopes()
    {
        var first = new StubSearchStrategy("first", FileBrowserSearchScope.LoadedFolder, canSearch: true);
        var duplicate = new StubSearchStrategy("second", FileBrowserSearchScope.LoadedFolder, canSearch: true);
        Assert.Throws<ArgumentException>(() => new FileBrowserSearchStrategyCatalog([first, duplicate]));

        var unavailable = new StubSearchStrategy("native", FileBrowserSearchScope.Provider, canSearch: false);
        var catalog = new FileBrowserSearchStrategyCatalog([first, unavailable]);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor());

        Assert.Same(first, catalog.Get(FileBrowserSearchScope.LoadedFolder, provider));
        var unavailableError = Assert.Throws<FileBrowserProviderException>(() => catalog.Get(
            FileBrowserSearchScope.Provider,
            provider));
        var unknownError = Assert.Throws<FileBrowserProviderException>(() => catalog.Get(
            FileBrowserSearchScope.Progressive,
            provider));

        Assert.Equal(FileBrowserErrorCode.Unsupported, unavailableError.Error.Code);
        Assert.Equal(FileBrowserErrorCode.Unsupported, unknownError.Error.Code);
        Assert.Equal([FileBrowserSearchScope.LoadedFolder], catalog.GetAvailable(provider));
    }

    [Fact]
    public void DefaultSearchCatalogAdvertisesOnlyScopesAllowedByProviderDescriptor()
    {
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(
            searchScopes: [FileBrowserSearchScope.Progressive, FileBrowserSearchScope.LoadedFolder]));
        var catalog = FileBrowserSearchStrategyCatalog.CreateDefault();

        Assert.Equal(
            [FileBrowserSearchScope.LoadedFolder, FileBrowserSearchScope.Progressive],
            catalog.GetAvailable(provider));
    }

    private sealed class StubSearchStrategy(
        string id,
        FileBrowserSearchScope scope,
        bool canSearch) : IFileBrowserSearchStrategy
    {
        public string Id { get; } = id;

        public FileBrowserSearchScope Scope { get; } = scope;

        public bool CanSearch(IFileBrowserProvider provider) => canSearch;

        public ValueTask<FileBrowserSearchPage> SearchAsync(
            FileBrowserSearchStrategyContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileBrowserSearchPage([], Id));
    }
}

