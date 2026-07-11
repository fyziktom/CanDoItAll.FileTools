namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class FileBrowserAccumulatorAndStateStoreTests
{
    [Fact]
    public void Accumulator_AppendDeduplicatesOccurrenceAndKeepsNewestDescriptor()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserItem first = TestFileBrowserFactory.File("file", root.Key, "old.txt", size: 1);
        FileBrowserItem updated = TestFileBrowserFactory.File("file", root.Key, "new.txt", size: 2);
        FileBrowserBrowseRequest request = TestFileBrowserFactory.BrowseRequest(root.Key);
        FileBrowserContainerAccumulator accumulator = FileBrowserContainerAccumulator.Start(
            request,
            new FileBrowserPage([first], "next", 1, "v1", FileBrowserCompleteness.Partial));

        accumulator.ApplyPage(
            request.Next("next", "v1"),
            new FileBrowserPage([updated], totalCount: 1, consistencyToken: "v1"),
            FileBrowserPageApplyMode.Append);

        FileBrowserContainerSnapshot snapshot = accumulator.Snapshot();
        FileBrowserItem item = Assert.Single(snapshot.Items);
        Assert.Equal("new.txt", item.Name);
        Assert.Equal(2, item.Size);
        Assert.Equal(2, snapshot.LoadedPageCount);
        Assert.Null(snapshot.NextContinuationToken);
    }

    [Fact]
    public void Accumulator_AppendRejectsMismatchedAndRepeatedCursorsWithoutMutation()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserBrowseRequest request = TestFileBrowserFactory.BrowseRequest(root.Key);
        FileBrowserContainerAccumulator accumulator = FileBrowserContainerAccumulator.Start(
            request,
            new FileBrowserPage([], "next", consistencyToken: "v1"));
        FileBrowserContainerSnapshot before = accumulator.Snapshot();

        FileBrowserProviderException mismatch = Assert.Throws<FileBrowserProviderException>(() =>
            accumulator.ApplyPage(
                request.Next("wrong", "v1"),
                new FileBrowserPage([], consistencyToken: "v1"),
                FileBrowserPageApplyMode.Append));
        FileBrowserProviderException cycle = Assert.Throws<FileBrowserProviderException>(() =>
            accumulator.ApplyPage(
                request.Next("next", "v1"),
                new FileBrowserPage([], "next", consistencyToken: "v1"),
                FileBrowserPageApplyMode.Append));

        Assert.Equal(FileBrowserErrorCode.StaleCursor, mismatch.Error.Code);
        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, cycle.Error.Code);
        Assert.Equal(before, accumulator.Snapshot());
    }

    [Fact]
    public void Accumulator_RetainedSnapshotRejectsAnyPreviouslyObservedCursor()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserBrowseRequest request = TestFileBrowserFactory.BrowseRequest(root.Key);
        FileBrowserContainerAccumulator accumulator = FileBrowserContainerAccumulator.Start(
            request,
            new FileBrowserPage([], "cursor-a", consistencyToken: "v1"));
        accumulator.ApplyPage(
            request.Next("cursor-a", "v1"),
            new FileBrowserPage([], "cursor-b", consistencyToken: "v1"),
            FileBrowserPageApplyMode.Append);
        var store = new BoundedFileBrowserStateStore();
        store.StoreContainer(accumulator.Snapshot());
        Assert.True(store.TryGetContainer(request, out FileBrowserContainerSnapshot? retainedSnapshot));
        FileBrowserContainerAccumulator retained = FileBrowserContainerAccumulator.FromSnapshot(
            retainedSnapshot!);
        FileBrowserContainerSnapshot before = retained.Snapshot();

        FileBrowserProviderException exception = Assert.Throws<FileBrowserProviderException>(() =>
            retained.ApplyPage(
                request.Next("cursor-b", "v1"),
                new FileBrowserPage([], "cursor-a", consistencyToken: "v1"),
                FileBrowserPageApplyMode.Append));

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, exception.Error.Code);
        Assert.Equal(before, retained.Snapshot());
    }

    [Fact]
    public void Accumulator_AppendPreservesPartialCompletenessAndMergesWarnings()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserBrowseRequest request = TestFileBrowserFactory.BrowseRequest(root.Key);
        var firstWarning = new FileBrowserPageWarning("first", "first warning");
        var secondWarning = new FileBrowserPageWarning("second", "second warning");
        FileBrowserContainerAccumulator accumulator = FileBrowserContainerAccumulator.Start(
            request,
            new FileBrowserPage(
                [],
                "next",
                consistencyToken: "v1",
                completeness: FileBrowserCompleteness.Partial,
                warnings: [firstWarning]));

        accumulator.ApplyPage(
            request.Next("next", "v1"),
            new FileBrowserPage(
                [],
                consistencyToken: "v1",
                completeness: FileBrowserCompleteness.Complete,
                warnings: [firstWarning, secondWarning]),
            FileBrowserPageApplyMode.Append);

        FileBrowserContainerSnapshot snapshot = accumulator.Snapshot();
        Assert.Equal(FileBrowserCompleteness.Partial, snapshot.Completeness);
        Assert.Equal([firstWarning, secondWarning], snapshot.Warnings);
    }

    [Fact]
    public void Accumulator_RejectsForeignItemsBeforeChangingActiveResult()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserItem current = TestFileBrowserFactory.File("current", root.Key);
        FileBrowserBrowseRequest request = TestFileBrowserFactory.BrowseRequest(root.Key);
        FileBrowserContainerAccumulator accumulator = FileBrowserContainerAccumulator.Start(
            request,
            new FileBrowserPage([current]));
        FileBrowserContainerSnapshot before = accumulator.Snapshot();
        FileBrowserItem foreign = TestFileBrowserFactory.File(
            "foreign",
            TestFileBrowserFactory.Key("foreign-root", "other"),
            source: "other");

        FileBrowserProviderException error = Assert.Throws<FileBrowserProviderException>(() =>
            accumulator.ApplyPage(request, new FileBrowserPage([foreign]), FileBrowserPageApplyMode.Replace));

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, error.Error.Code);
        FileBrowserContainerSnapshot after = accumulator.Snapshot();
        Assert.Equal(before.QueryKey, after.QueryKey);
        Assert.Equal(before.Items.Select(item => item.Key), after.Items.Select(item => item.Key));
        Assert.Equal(before.LoadedPageCount, after.LoadedPageCount);
        Assert.Equal(before.NextContinuationToken, after.NextContinuationToken);
    }

    [Fact]
    public void BoundedStore_RetainsIndependentQueryVariants()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserBrowseRequest byName = TestFileBrowserFactory.BrowseRequest(root.Key);
        FileBrowserBrowseRequest bySize = TestFileBrowserFactory.BrowseRequest(
            root.Key,
            sort: new FileBrowserSortDescriptor(FileBrowserSortField.Size));
        var store = new BoundedFileBrowserStateStore();
        store.StoreContainer(CreateSnapshot(byName, TestFileBrowserFactory.File("name", root.Key)));
        store.StoreContainer(CreateSnapshot(bySize, TestFileBrowserFactory.File("size", root.Key)));

        Assert.True(store.TryGetContainer(byName, out FileBrowserContainerSnapshot? nameSnapshot));
        Assert.True(store.TryGetContainer(bySize, out FileBrowserContainerSnapshot? sizeSnapshot));
        Assert.Equal("name", Assert.Single(nameSnapshot!.Items).Key.Value);
        Assert.Equal("size", Assert.Single(sizeSnapshot!.Items).Key.Value);
        Assert.Equal(2, store.GetDiagnostics().CachedContainerQueryCount);
    }

    [Fact]
    public void BoundedStore_UsesLeastRecentlyUsedEvictionAndProtectsCurrentPath()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserItem a = TestFileBrowserFactory.Container("a", root.Key);
        FileBrowserItem b = TestFileBrowserFactory.Container("b", root.Key);
        FileBrowserItem c = TestFileBrowserFactory.Container("c", root.Key);
        var store = new BoundedFileBrowserStateStore(
            new FileBrowserTreeStoreOptions(maximumContainers: 2, maximumItems: 20));
        FileBrowserBrowseRequest requestA = TestFileBrowserFactory.BrowseRequest(a.Key);
        FileBrowserBrowseRequest requestB = TestFileBrowserFactory.BrowseRequest(b.Key);
        FileBrowserBrowseRequest requestC = TestFileBrowserFactory.BrowseRequest(c.Key);
        store.StoreContainer(CreateSnapshot(requestA));
        store.StoreContainer(CreateSnapshot(requestB));
        store.SetProtectedPath([a.Key]);

        store.StoreContainer(CreateSnapshot(requestC));

        Assert.True(store.TryGetContainer(requestA, out _));
        Assert.False(store.TryGetContainer(requestB, out _));
        Assert.True(store.TryGetContainer(requestC, out _));
        Assert.Equal(1, store.GetDiagnostics().EvictedContainerQueryCount);
    }

    [Fact]
    public void BoundedStore_LoadedDescendantsAreBreadthFirstAndCycleSafe()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserItem a = TestFileBrowserFactory.Container("a", root.Key);
        FileBrowserItem b = TestFileBrowserFactory.Container("b", root.Key);
        FileBrowserItem leaf = TestFileBrowserFactory.File("leaf", a.Key);
        var store = new BoundedFileBrowserStateStore();
        store.StoreContainer(CreateSnapshot(TestFileBrowserFactory.BrowseRequest(root.Key), a, b));
        store.StoreContainer(CreateSnapshot(TestFileBrowserFactory.BrowseRequest(a.Key), leaf));

        IReadOnlyList<FileBrowserItem> descendants = store.GetLoadedDescendants(root.Key);

        Assert.Equal([a.Key, b.Key, leaf.Key], descendants.Select(item => item.Key));
    }

    [Fact]
    public void BoundedStore_InvalidateItemRemovesContainingAndChildQueries()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserItem folder = TestFileBrowserFactory.Container("folder", root.Key);
        FileBrowserBrowseRequest rootRequest = TestFileBrowserFactory.BrowseRequest(root.Key);
        FileBrowserBrowseRequest folderRequest = TestFileBrowserFactory.BrowseRequest(folder.Key);
        var store = new BoundedFileBrowserStateStore();
        store.StoreContainer(CreateSnapshot(rootRequest, folder));
        store.StoreContainer(CreateSnapshot(folderRequest, TestFileBrowserFactory.File("leaf", folder.Key)));

        store.InvalidateItem(folder.Key);

        Assert.False(store.TryGetContainer(rootRequest, out _));
        Assert.False(store.TryGetContainer(folderRequest, out _));
        Assert.False(store.TryGetItem(folder.Key, out _));
    }

    [Fact]
    public void BoundedStore_InvalidateSourceTargetsOnlyRequestedSource()
    {
        FileBrowserItem firstRoot = TestFileBrowserFactory.Container("root", source: "first");
        FileBrowserItem secondRoot = TestFileBrowserFactory.Container("root", source: "second");
        FileBrowserBrowseRequest firstRequest = TestFileBrowserFactory.BrowseRequest(firstRoot.Key);
        FileBrowserBrowseRequest secondRequest = TestFileBrowserFactory.BrowseRequest(secondRoot.Key);
        var store = new BoundedFileBrowserStateStore();
        store.StoreContainer(CreateSnapshot(firstRequest));
        store.StoreContainer(CreateSnapshot(secondRequest));

        store.InvalidateSource(firstRoot.Key.SourceId);

        Assert.False(store.TryGetContainer(firstRequest, out _));
        Assert.True(store.TryGetContainer(secondRequest, out _));
    }

    [Fact]
    public void BoundedStore_InvalidateAllClearsReusableState()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserBrowseRequest request = TestFileBrowserFactory.BrowseRequest(root.Key);
        var store = new BoundedFileBrowserStateStore();
        store.StoreContainer(CreateSnapshot(request, TestFileBrowserFactory.File("leaf", root.Key)));

        store.InvalidateAll();

        Assert.False(store.TryGetContainer(request, out _));
        Assert.Equal(FileBrowserTreeDiagnostics.Empty, store.GetDiagnostics());
    }

    [Fact]
    public void DisabledStore_NeverRetainsAndAlwaysReportsZeroDiagnostics()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        FileBrowserBrowseRequest request = TestFileBrowserFactory.BrowseRequest(root.Key);
        var store = new DisabledFileBrowserStateStore();

        store.StorePath([root]);
        store.StoreContainer(CreateSnapshot(request, TestFileBrowserFactory.File("leaf", root.Key)));
        store.SetProtectedPath([root.Key]);

        Assert.False(store.TryGetContainer(request, out _));
        Assert.False(store.TryGetItem(root.Key, out _));
        Assert.Empty(store.GetLoadedChildren(root.Key));
        Assert.Empty(store.GetLoadedDescendants(root.Key));
        Assert.Equal(FileBrowserTreeDiagnostics.Empty, store.GetDiagnostics());
    }

    private static FileBrowserContainerSnapshot CreateSnapshot(
        FileBrowserBrowseRequest request,
        params FileBrowserItem[] items)
        => FileBrowserContainerAccumulator.Start(request, new FileBrowserPage(items)).Snapshot();
}
