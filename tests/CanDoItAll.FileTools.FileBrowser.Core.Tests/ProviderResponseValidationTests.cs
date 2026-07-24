namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class ProviderResponseValidationTests
{
    [Fact]
    public async Task NativeSearchRejectsForeignSourceBeforeReturningThePage()
    {
        var request = CreateSearchRequest();
        var page = new FileBrowserSearchPage(
            [TestFileBrowserFactory.File("foreign", source: "other")],
            "native-index");

        FileBrowserProviderException exception = await SearchMalformedAsync(request, page);

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, exception.Error.Code);
    }

    [Fact]
    public async Task NativeSearchRejectsOversizedAndImpossibleCountPages()
    {
        var request = CreateSearchRequest(pageSize: 1);
        var oversized = new FileBrowserSearchPage(
            [
                TestFileBrowserFactory.File("one", request.ContainerKey),
                TestFileBrowserFactory.File("two", request.ContainerKey)
            ],
            "native-index",
            totalCount: 2);
        var impossibleCount = new FileBrowserSearchPage(
            [
                TestFileBrowserFactory.File("one", request.ContainerKey),
                TestFileBrowserFactory.File("two", request.ContainerKey)
            ],
            "native-index",
            totalCount: 1);

        FileBrowserProviderException oversizedException = await SearchMalformedAsync(request, oversized);
        FileBrowserProviderException countException = await SearchMalformedAsync(
            CreateSearchRequest(pageSize: 2),
            impossibleCount);

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, oversizedException.Error.Code);
        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, countException.Error.Code);
    }

    [Fact]
    public async Task NativeSearchRejectsEquivalentAndConflictingDuplicateOccurrences()
    {
        var request = CreateSearchRequest(pageSize: 2);
        var first = TestFileBrowserFactory.File("same", request.ContainerKey, "Same");
        var equivalent = TestFileBrowserFactory.File("same", request.ContainerKey, "Same");
        var conflicting = TestFileBrowserFactory.File("same", request.ContainerKey, "Changed");

        FileBrowserProviderException duplicateException = await SearchMalformedAsync(
            request,
            new FileBrowserSearchPage([first, equivalent], "native-index"));
        FileBrowserProviderException conflictException = await SearchMalformedAsync(
            request,
            new FileBrowserSearchPage([first, conflicting], "native-index"));

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, duplicateException.Error.Code);
        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, conflictException.Error.Code);
    }

    [Fact]
    public async Task NativeSearchRejectsNonAdvancingCursorAndContinuationRevisionChange()
    {
        var request = CreateSearchRequest(
            continuationToken: "cursor-2",
            consistencyToken: "revision-1");
        var repeatedCursor = new FileBrowserSearchPage(
            [TestFileBrowserFactory.File("same", request.ContainerKey)],
            "native-index",
            nextContinuationToken: "cursor-2",
            consistencyToken: "revision-1");
        var changedRevision = new FileBrowserSearchPage(
            [TestFileBrowserFactory.File("same", request.ContainerKey)],
            "native-index",
            consistencyToken: "revision-2");

        FileBrowserProviderException cursorException = await SearchMalformedAsync(request, repeatedCursor);
        FileBrowserProviderException revisionException = await SearchMalformedAsync(request, changedRevision);

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, cursorException.Error.Code);
        Assert.Equal(FileBrowserErrorCode.StaleCursor, revisionException.Error.Code);
        Assert.True(revisionException.Error.IsRetryable);
    }

    [Fact]
    public async Task MalformedNativeSearchDoesNotReplaceTheVisibleBrowseState()
    {
        var root = TestFileBrowserFactory.Container("root");
        var browseItem = TestFileBrowserFactory.File("browse-item", root.Key);
        var descriptor = NativeDescriptor(maximumPageSize: 2);
        var provider = new FakeFileBrowserProvider(descriptor)
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([browseItem])),
            SearchHandler = (_, _) => ValueTask.FromResult(new FileBrowserSearchPage(
                [
                    TestFileBrowserFactory.File("one", root.Key),
                    TestFileBrowserFactory.File("two", root.Key),
                    TestFileBrowserFactory.File("three", root.Key)
                ],
                "native-index"))
        };
        await using var session = new FileBrowserSession(
            [provider],
            new FileBrowserSessionOptions(pageSize: 2));
        await session.InitializeAsync();

        await session.SearchAsync("result", FileBrowserSearchScope.Provider);

        Assert.Equal(root.Key, session.Snapshot.CurrentContainer!.Key);
        Assert.Equal([browseItem.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Null(session.Snapshot.Search);
        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, session.Snapshot.Error!.Code);
    }

    private static async Task<FileBrowserProviderException> SearchMalformedAsync(
        FileBrowserSearchRequest request,
        FileBrowserSearchPage page)
    {
        var provider = new FakeFileBrowserProvider(NativeDescriptor())
        {
            SearchHandler = (_, _) => ValueTask.FromResult(page)
        };
        var strategy = new ProviderFileBrowserSearchStrategy();
        return await Assert.ThrowsAsync<FileBrowserProviderException>(() => strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(
                provider,
                new EmptySearchData(),
                request)).AsTask());
    }

    private static FileBrowserSearchRequest CreateSearchRequest(
        int pageSize = 2,
        string? continuationToken = null,
        string? consistencyToken = null)
        => new(
            TestFileBrowserFactory.Key("root"),
            "result",
            FileBrowserSearchScope.Provider,
            pageSize,
            continuationToken,
            consistencyToken: consistencyToken);

    private static FileBrowserSourceDescriptor NativeDescriptor(int maximumPageSize = 100)
        => TestFileBrowserFactory.Descriptor(
            capabilities: FileBrowserSourceCapabilities.PagedBrowse
                | FileBrowserSourceCapabilities.NativeSearch,
            maximumPageSize: maximumPageSize,
            searchScopes: [FileBrowserSearchScope.Provider]);

    private sealed class EmptySearchData : IFileBrowserSearchData
    {
        public IReadOnlyList<FileBrowserItem> CurrentItems => [];

        public ValueTask<FileBrowserPage> BrowseAndCacheAsync(
            FileBrowserBrowseRequest request,
            FileBrowserPageApplyMode applyMode,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Native search must not browse.");

        public bool TryGetItem(FileBrowserItemKey key, out FileBrowserItem? item)
        {
            item = null;
            return false;
        }

        public IReadOnlyList<FileBrowserItem> GetLoadedChildren(FileBrowserItemKey parentKey) => [];

        public IReadOnlyList<FileBrowserItem> GetLoadedDescendants(FileBrowserItemKey parentKey) => [];
    }
}

