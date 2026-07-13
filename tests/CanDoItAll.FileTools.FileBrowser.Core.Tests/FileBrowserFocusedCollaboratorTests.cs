namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class FileBrowserFocusedCollaboratorTests
{
    [Fact]
    public void ItemOrdering_ProviderNative_PreservesInputOrder()
    {
        FileBrowserItem[] input =
        [
            TestFileBrowserFactory.File("z-last"),
            TestFileBrowserFactory.Container("a-first")
        ];

        IReadOnlyList<FileBrowserItem> result = FileBrowserItemOrdering.Apply(
            input,
            new FileBrowserSortDescriptor(
                FileBrowserSortField.ProviderNative,
                FileBrowserSortDirection.Descending,
                FoldersFirst: true));

        Assert.Equal(input, result);
    }

    [Fact]
    public void SessionOptions_RejectUndefinedRetentionMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileBrowserSessionOptions(
            retentionMode: (FileBrowserStateRetentionMode)int.MaxValue));
    }

    [Fact]
    public void SearchBudget_RejectsInvalidDurationConcurrencyMatchAndByteLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileBrowserSearchBudget(
            maximumDuration: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileBrowserSearchBudget(
            maximumConcurrentRequests: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileBrowserSearchBudget(
            maximumMatches: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileBrowserSearchBudget(
            maximumRetainedBytes: 0));
    }

    [Fact]
    public async Task EmptySourceSet_IsRenderableButCannotBeInitialized()
    {
        await using var session = new FileBrowserSession(new FileBrowserSourceSet("empty", []));

        Assert.Empty(session.Snapshot.Sources);
        Assert.Null(session.Snapshot.CurrentSource);
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = session.InitializeAsync();
        });
    }

    [Fact]
    public async Task Loader_BoundedRetentionReusesCompletedContainerWithoutProviderIo()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        string name = "before.txt";
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            BrowseHandler = (request, _) => ValueTask.FromResult(
                new FileBrowserPage([TestFileBrowserFactory.File("leaf", request.ParentKey, name)]))
        };
        var loader = new FileBrowserLoader(new BoundedFileBrowserStateStore());
        var location = new FileBrowserLocation([root]);

        FileBrowserLoadedContainer first = await loader.LoadAsync(
            provider,
            location,
            new FileBrowserSortDescriptor(),
            FileBrowserFilter.None,
            includeDescendants: false,
            force: false);
        name = "after.txt";
        FileBrowserLoadedContainer second = await loader.LoadAsync(
            provider,
            location,
            new FileBrowserSortDescriptor(),
            FileBrowserFilter.None,
            includeDescendants: false,
            force: false);

        Assert.Equal(1, provider.BrowseCallCount);
        Assert.Equal("before.txt", Assert.Single(first.Snapshot.Items).Name);
        Assert.Equal("before.txt", Assert.Single(second.Snapshot.Items).Name);
        Assert.True(second.WasRetained);
    }

    [Fact]
    public async Task Loader_DisabledRetentionObservesProviderMutationOnEveryRevisit()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        string name = "before.txt";
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            BrowseHandler = (request, _) => ValueTask.FromResult(
                new FileBrowserPage([TestFileBrowserFactory.File("leaf", request.ParentKey, name)]))
        };
        var store = new DisabledFileBrowserStateStore();
        var loader = new FileBrowserLoader(store);
        var location = new FileBrowserLocation([root]);

        FileBrowserLoadedContainer first = await loader.LoadAsync(
            provider,
            location,
            new FileBrowserSortDescriptor(),
            FileBrowserFilter.None,
            includeDescendants: false,
            force: false);
        name = "after.txt";
        FileBrowserLoadedContainer second = await loader.LoadAsync(
            provider,
            location,
            new FileBrowserSortDescriptor(),
            FileBrowserFilter.None,
            includeDescendants: false,
            force: false);

        Assert.Equal(2, provider.BrowseCallCount);
        Assert.Equal("before.txt", Assert.Single(first.Snapshot.Items).Name);
        Assert.Equal("after.txt", Assert.Single(second.Snapshot.Items).Name);
        Assert.False(second.WasRetained);
        Assert.Equal(FileBrowserTreeDiagnostics.Empty, store.GetDiagnostics());
    }

    [Fact]
    public async Task Loader_CancellationCannotCommitReturnedPageIntoRetention()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            BrowseHandler = (request, _) => ValueTask.FromResult(
                new FileBrowserPage([TestFileBrowserFactory.File("leaf", request.ParentKey)]))
        };
        var store = new BoundedFileBrowserStateStore();
        var loader = new FileBrowserLoader(store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await loader.LoadAsync(
                provider,
                new FileBrowserLocation([root]),
                new FileBrowserSortDescriptor(),
                FileBrowserFilter.None,
                includeDescendants: false,
                force: false,
                cancellation.Token));

        Assert.Equal(0, store.GetDiagnostics().CachedContainerQueryCount);
    }

    [Fact]
    public async Task Navigator_ResolvesAndValidatesPathWithoutLoaderOrSessionConstruction()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserItem folder = TestFileBrowserFactory.Container("folder", root.Key);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            PathHandler = (_, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root, folder])
        };
        var navigator = new FileBrowserNavigator();

        FileBrowserNavigationTarget target = await navigator.ResolveAsync(
            new FileBrowserSourceSet("r1", [provider]),
            folder.Key,
            FileBrowserMetadataRequest.Standard);

        Assert.Same(provider, target.Provider);
        Assert.Equal(folder.Key, target.Location.Key);
        Assert.Null(navigator.Current);
        navigator.Reset(target.Location);
        Assert.Equal(folder.Key, navigator.Current!.Key);
    }

    [Fact]
    public void SelectionState_RejectsInvisibleItemsAndReconcilesRemovedOccurrences()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserItem first = TestFileBrowserFactory.File("first", root.Key);
        FileBrowserItem second = TestFileBrowserFactory.File("second", root.Key);
        var selection = new FileBrowserSelectionState();

        selection.Select([first, second], first.Key);
        selection.Select([first, second], second.Key, toggle: true);
        selection.Reconcile([second]);

        Assert.Equal([second.Key], selection.Snapshot());
        Assert.Throws<ArgumentException>(() => selection.Select([second], first.Key));
    }

    [Fact]
    public async Task ActionDispatcher_ProjectsAndExecutesBuiltInsWithoutSessionConstruction()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        var item = new FileBrowserItem(
            TestFileBrowserFactory.Key("file"),
            root.Key,
            "readme.md",
            FileBrowserItemKind.File,
            FileBrowserItemCategory.Document,
            displayPath: "/readme.md",
            childState: FileBrowserChildState.Empty,
            capabilities: FileBrowserItemCapabilities.Open
                | FileBrowserItemCapabilities.CopyPath,
            openUri: "/files/readme");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor());
        var dispatcher = new FileBrowserActionDispatcher();

        IReadOnlyList<FileBrowserActionDescriptor> actions = await dispatcher.GetActionsAsync(provider, item);
        FileBrowserActionDispatch copy = await dispatcher.DispatchAsync(
            provider,
            item,
            new FileBrowserActionRequest(item.Key, FileBrowserActionIds.CopyPath));

        Assert.Equal([FileBrowserActionIds.Open, FileBrowserActionIds.CopyPath], actions.Select(value => value.Id));
        Assert.Equal("/readme.md", copy.Result!.Value);
        Assert.Null(copy.NavigationKey);
    }

    [Fact]
    public async Task SearchCoordinator_SelectsNativeStrategyAndPublishesValidatedResult()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserItem match = TestFileBrowserFactory.File("match", root.Key, "match.txt");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(
            capabilities: FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.NativeSearch,
            searchScopes: [FileBrowserSearchScope.Provider]))
        {
            SearchHandler = (_, _) => ValueTask.FromResult(
                new FileBrowserSearchPage([match], "native"))
        };
        var loader = new FileBrowserLoader(new BoundedFileBrowserStateStore());
        var coordinator = new FileBrowserSearchCoordinator(loader);
        FileBrowserBrowseRequest request = TestFileBrowserFactory.BrowseRequest(root.Key);
        FileBrowserContainerSnapshot active = FileBrowserContainerAccumulator
            .Start(request, new FileBrowserPage([]))
            .Snapshot();

        await coordinator.SearchAsync(
            provider,
            new FileBrowserLocation([root]),
            active,
            "match",
            FileBrowserSearchScope.Provider,
            new FileBrowserSortDescriptor(),
            FileBrowserFilter.None);

        Assert.Equal("native", coordinator.Snapshot!.StrategyId);
        Assert.Equal(match.Key, Assert.Single(coordinator.VisibleItems).Key);
        Assert.Equal(1, provider.SearchCallCount);
    }

    [Fact]
    public async Task SourceTransitionCoordinator_StagesCompleteStateWithoutReusableRetention()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root", source: "updated");
        FileBrowserItem file = TestFileBrowserFactory.File(
            "file",
            root.Key,
            "updated.txt",
            source: "updated");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("updated"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([file]))
        };
        var coordinator = new FileBrowserSourceTransitionCoordinator(new FileBrowserSessionOptions());

        FileBrowserStagedSourceTransition transition = await coordinator.StageAsync(
            new FileBrowserSourceSet("r2", [provider]),
            currentSource: null,
            currentLocation: null,
            CancellationToken.None);

        Assert.Same(provider, transition.Target!.Provider);
        Assert.Equal(root.Key, transition.Target.Location.Key);
        Assert.Equal(file.Key, Assert.Single(transition.Container!.Snapshot.Items).Key);
        Assert.False(transition.Container.WasRetained);
        Assert.Equal(1, provider.BrowseCallCount);
    }

    [Fact]
    public async Task ModeCoordinator_InvalidatedSearchRefreshesBrowseBeforeReapplyingSearch()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        string currentName = "before.txt";
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            BrowseHandler = (request, _) => ValueTask.FromResult(new FileBrowserPage([
                TestFileBrowserFactory.File("file", request.ParentKey, currentName)
            ]))
        };
        var options = new FileBrowserSessionOptions(
            retentionMode: FileBrowserStateRetentionMode.Disabled);
        var loader = new FileBrowserLoader(new DisabledFileBrowserStateStore(), options);
        var navigator = new FileBrowserNavigator();
        var location = new FileBrowserLocation([root]);
        navigator.Reset(location);
        var selection = new FileBrowserSelectionState();
        var search = new FileBrowserSearchCoordinator(loader, options: options);
        var workspace = new FileBrowserBrowseWorkspace(options.DefaultSort)
        {
            Provider = provider,
            Container = await loader.LoadAsync(
                provider,
                location,
                options.DefaultSort,
                FileBrowserFilter.None,
                includeDescendants: false,
                force: false)
        };
        var coordinator = new FileBrowserModeCoordinator(
            loader,
            search,
            selection,
            workspace,
            navigator);
        await coordinator.SearchAsync(".txt", FileBrowserSearchScope.LoadedFolder, CancellationToken.None);
        currentName = "after.txt";
        coordinator.InvalidateSource(provider.Descriptor.Id);
        int beforeRefresh = provider.BrowseCallCount;

        await coordinator.RefreshAsync(CancellationToken.None);

        Assert.Equal(beforeRefresh + 1, provider.BrowseCallCount);
        Assert.Equal("after.txt", Assert.Single(coordinator.CurrentItems).Name);
    }

    [Fact]
    public void Ordering_KeepsFoldersFirstThenAppliesDirectionAndStableTies()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserItem folder = TestFileBrowserFactory.Container("folder", root.Key, "Z folder");
        FileBrowserItem small = TestFileBrowserFactory.File("small", root.Key, "A.txt", size: 1);
        FileBrowserItem large = TestFileBrowserFactory.File("large", root.Key, "B.txt", size: 10);

        IReadOnlyList<FileBrowserItem> result = FileBrowserItemOrdering.Apply(
            [small, large, folder],
            new FileBrowserSortDescriptor(
                FileBrowserSortField.Size,
                FileBrowserSortDirection.Descending,
                FoldersFirst: true));

        Assert.Equal([folder.Key, large.Key, small.Key], result.Select(item => item.Key));
    }

    [Fact]
    public void Ordering_UsesAscendingNameTieBreakerForDescendingField()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        DateTimeOffset modified = DateTimeOffset.UtcNow;
        FileBrowserItem beta = TestFileBrowserFactory.File("beta", root.Key, "Beta.txt", modifiedAt: modified);
        FileBrowserItem alpha = TestFileBrowserFactory.File("alpha", root.Key, "Alpha.txt", modifiedAt: modified);

        IReadOnlyList<FileBrowserItem> result = FileBrowserItemOrdering.Apply(
            [beta, alpha],
            new FileBrowserSortDescriptor(
                FileBrowserSortField.ModifiedAt,
                FileBrowserSortDirection.Descending));

        Assert.Equal([alpha.Key, beta.Key], result.Select(item => item.Key));
    }
}
