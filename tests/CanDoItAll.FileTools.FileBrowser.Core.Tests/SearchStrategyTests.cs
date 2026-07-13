using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class SearchStrategyTests
{
    private static readonly FileBrowserSourceId SourceId = new("search-tests");
    private static readonly FileBrowserItemKey RootKey = Key("root");

    [Fact]
    public async Task LoadedFolderFiltersOrdersAndPagesWithoutProviderIo()
    {
        var currentItems = new[]
        {
            File(
                "alpha",
                "Alpha.cs",
                size: 10,
                displayPath: "/needle/Alpha.cs",
                mediaType: "text/plain"),
            File(
                "zeta",
                "Zeta.cs",
                size: 30,
                mediaType: "text/x-csharp",
                contentIdentity: "NEEDLE-content-hash"),
            File(
                "wrong-media",
                "needle-beta.cs",
                size: 100,
                mediaType: "application/octet-stream"),
            File(
                "no-text-match",
                "boring.cs",
                size: 90,
                mediaType: "text/plain"),
            File(
                "wrong-extension",
                "needle-notes.md",
                category: FileBrowserItemCategory.Code,
                size: 80,
                mediaType: "text/markdown"),
            Container("matching-folder", "needle folder")
        };
        var provider = new RecordingProvider(CreateDescriptor());
        var data = new RecordingSearchData(currentItems: currentItems);
        var strategy = new LoadedFolderFileBrowserSearchStrategy();
        var sort = new FileBrowserSortDescriptor(
            FileBrowserSortField.Size,
            FileBrowserSortDirection.Descending,
            FoldersFirst: false);
        var filter = new FileBrowserFilter(
            kinds: [FileBrowserItemKind.File],
            categories: [FileBrowserItemCategory.Code],
            extensions: [".cs"],
            mediaTypePrefix: "text/");

        var firstRequest = SearchRequest(
            "needle",
            FileBrowserSearchScope.LoadedFolder,
            pageSize: 1,
            sort: sort,
            filter: filter,
            consistencyToken: "revision-7");
        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, firstRequest));
        FileBrowserSearchPage second = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, firstRequest.Next(first.NextContinuationToken!)));

        Assert.Equal("loaded-folder", first.StrategyId);
        Assert.Equal(["Zeta.cs"], Names(first));
        Assert.Equal("offset:1", first.NextContinuationToken);
        Assert.Equal(2L, first.TotalCount);
        Assert.False(first.IsPartial);
        Assert.Equal(0, first.ScannedContainers);
        Assert.Equal(currentItems.Length, first.ScannedItems);
        Assert.Equal("revision-7", first.ConsistencyToken);
        Assert.Empty(first.Warnings);

        Assert.Equal("loaded-folder", second.StrategyId);
        Assert.Equal(["Alpha.cs"], Names(second));
        Assert.Null(second.NextContinuationToken);
        Assert.Equal(2L, second.TotalCount);
        Assert.False(second.IsPartial);
        Assert.Equal(0, second.ScannedContainers);
        Assert.Equal(currentItems.Length, second.ScannedItems);
        Assert.Equal("revision-7", second.ConsistencyToken);
        Assert.Empty(second.Warnings);

        Assert.Equal(0, data.GetLoadedDescendantsCallCount);
        Assert.Empty(data.BrowseCalls);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task LoadedDescendantsFiltersOrdersAndPagesAsAnExplicitPartialScope()
    {
        DateTimeOffset dayOne = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var descendants = new[]
        {
            File("third", "target-third.cs", modifiedAt: dayOne.AddDays(2), mediaType: "text/plain"),
            File("first", "target-first.cs", modifiedAt: dayOne, mediaType: "text/plain"),
            File(
                "second",
                "second.cs",
                displayPath: "/loaded/TARGET/second.cs",
                modifiedAt: dayOne.AddDays(1),
                mediaType: "text/plain"),
            File("wrong-extension", "target.md", modifiedAt: dayOne.AddDays(3), mediaType: "text/plain"),
            File(
                "wrong-category",
                "target-category.cs",
                category: FileBrowserItemCategory.Document,
                modifiedAt: dayOne.AddDays(4),
                mediaType: "text/plain"),
            File("no-text-match", "ordinary.cs", modifiedAt: dayOne.AddDays(5), mediaType: "text/plain")
        };
        var provider = new RecordingProvider(CreateDescriptor());
        var data = new RecordingSearchData(loadedDescendants: descendants);
        var strategy = new LoadedDescendantsFileBrowserSearchStrategy();
        var request = SearchRequest(
            "target",
            FileBrowserSearchScope.LoadedDescendants,
            pageSize: 2,
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.ModifiedAt,
                FileBrowserSortDirection.Ascending,
                FoldersFirst: false),
            filter: new FileBrowserFilter(
                kinds: [FileBrowserItemKind.File],
                categories: [FileBrowserItemCategory.Code],
                extensions: ["cs"],
                mediaTypePrefix: "text/"),
            consistencyToken: "loaded-revision");

        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));
        FileBrowserSearchPage second = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request.Next(first.NextContinuationToken!)));

        Assert.Equal(["target-first.cs", "second.cs"], Names(first));
        Assert.Equal("offset:2", first.NextContinuationToken);
        Assert.Equal(3L, first.TotalCount);
        Assert.True(first.IsPartial);
        Assert.Equal(0, first.ScannedContainers);
        Assert.Equal(descendants.Length, first.ScannedItems);
        Assert.Equal("loaded-revision", first.ConsistencyToken);
        FileBrowserPageWarning firstWarning = Assert.Single(first.Warnings);
        Assert.Equal("loaded-scope", firstWarning.Code);
        Assert.Equal(
            "Results include only descendants already loaded in this browser session.",
            firstWarning.Message);

        Assert.Equal(["target-third.cs"], Names(second));
        Assert.Null(second.NextContinuationToken);
        Assert.Equal(3L, second.TotalCount);
        Assert.True(second.IsPartial);
        Assert.Equal(0, second.ScannedContainers);
        Assert.Equal(descendants.Length, second.ScannedItems);
        Assert.Equal("loaded-revision", second.ConsistencyToken);
        Assert.Equal(["loaded-scope"], second.Warnings.Select(warning => warning.Code));

        Assert.Equal(2, data.GetLoadedDescendantsCallCount);
        Assert.Empty(data.BrowseCalls);
        AssertNoProviderIo(provider);
    }

    [Theory]
    [InlineData(FileBrowserSearchScope.LoadedFolder, "cursor-from-provider")]
    [InlineData(FileBrowserSearchScope.LoadedFolder, "offset:-1")]
    [InlineData(FileBrowserSearchScope.LoadedDescendants, "not-an-offset")]
    [InlineData(FileBrowserSearchScope.LoadedDescendants, "offset:not-a-number")]
    public async Task LoadedSearchRejectsStaleTokensWithoutProviderIo(
        FileBrowserSearchScope scope,
        string continuationToken)
    {
        FileBrowserItem candidate = File("candidate", "match.cs", mediaType: "text/plain");
        var provider = new RecordingProvider(CreateDescriptor());
        var data = new RecordingSearchData(
            currentItems: [candidate],
            loadedDescendants: [candidate]);
        IFileBrowserSearchStrategy strategy = scope switch
        {
            FileBrowserSearchScope.LoadedFolder => new LoadedFolderFileBrowserSearchStrategy(),
            FileBrowserSearchScope.LoadedDescendants => new LoadedDescendantsFileBrowserSearchStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
        var request = SearchRequest("match", scope, continuationToken: continuationToken);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => strategy.SearchAsync(
                new FileBrowserSearchStrategyContext(provider, data, request)).AsTask());

        Assert.Equal(FileBrowserErrorCode.StaleCursor, exception.Error.Code);
        Assert.Equal("The loaded-search continuation token is invalid.", exception.Error.Message);
        Assert.True(exception.Error.IsRetryable);
        Assert.Empty(data.BrowseCalls);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public void ProviderNativeCapabilityRequiresInterfaceFlagAndScope()
    {
        var strategy = new ProviderFileBrowserSearchStrategy();
        var capable = new NativeRecordingProvider(CreateDescriptor(
            FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.NativeSearch,
            [FileBrowserSearchScope.Provider]));
        var missingCapability = new NativeRecordingProvider(CreateDescriptor(
            FileBrowserSourceCapabilities.PagedBrowse,
            [FileBrowserSearchScope.Provider]));
        var missingInterface = new RecordingProvider(CreateDescriptor(
            FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.NativeSearch,
            [FileBrowserSearchScope.Provider]));
        var missingScope = new NativeRecordingProvider(CreateDescriptor(
            FileBrowserSourceCapabilities.PagedBrowse,
            [FileBrowserSearchScope.Progressive]));

        Assert.Equal("provider-native", strategy.Id);
        Assert.Equal(FileBrowserSearchScope.Provider, strategy.Scope);
        Assert.True(strategy.CanSearch(capable));
        Assert.False(strategy.CanSearch(missingCapability));
        Assert.False(strategy.CanSearch(missingInterface));
        Assert.False(strategy.CanSearch(missingScope));
    }

    [Fact]
    public async Task ProviderNativeDelegatesExactRequestTokenAndResultWithoutBrowsing()
    {
        var expectedPage = new FileBrowserSearchPage(
            [File("native-result", "native-match.cs")],
            "provider-index-v2",
            nextContinuationToken: "native-next",
            totalCount: 8,
            consistencyToken: "native-revision");
        var provider = new NativeRecordingProvider(
            CreateDescriptor(
                FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.NativeSearch,
                [FileBrowserSearchScope.Provider]),
            (_, _) => ValueTask.FromResult(expectedPage));
        var data = new RecordingSearchData();
        var request = SearchRequest(
            "native",
            FileBrowserSearchScope.Provider,
            pageSize: 17,
            continuationToken: "native-cursor",
            consistencyToken: "native-revision");
        using var cancellation = new CancellationTokenSource();

        FileBrowserSearchPage actual = await new ProviderFileBrowserSearchStrategy().SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request),
            cancellation.Token);

        Assert.Same(expectedPage, actual);
        SearchCall call = Assert.Single(provider.SearchCalls);
        Assert.Same(request, call.Request);
        Assert.Equal(cancellation.Token, call.CancellationToken);
        Assert.Empty(data.BrowseCalls);
        Assert.Equal(0, data.GetLoadedDescendantsCallCount);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProviderNativeRejectsAProviderThatDoesNotImplementSearch()
    {
        var provider = new RecordingProvider(CreateDescriptor(
            FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.NativeSearch,
            [FileBrowserSearchScope.Provider]));
        var data = new RecordingSearchData();
        var request = SearchRequest("match", FileBrowserSearchScope.Provider);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => new ProviderFileBrowserSearchStrategy().SearchAsync(
                new FileBrowserSearchStrategyContext(provider, data, request)).AsTask());

        Assert.Equal(FileBrowserErrorCode.Unsupported, exception.Error.Code);
        Assert.Equal("This source does not implement native search.", exception.Error.Message);
        Assert.False(exception.Error.IsRetryable);
        Assert.Empty(data.BrowseCalls);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveSearchDrainsPagesAndVisitsContainersBreadthFirst()
    {
        FileBrowserItem folderB = Container("folder-b", "Folder B");
        FileBrowserItem folderA = Container("folder-a", "Folder A");
        FileBrowserItem folderB1 = Container("folder-b-1", "Folder B1", folderB.Key);
        var pages = new[]
        {
            new FileBrowserPage(
                [folderB, File("root-z", "hit-root-z", RootKey)],
                nextContinuationToken: "root-page-2",
                warnings: [new FileBrowserPageWarning("root-page-1", "Root page one warning.")]),
            new FileBrowserPage(
                [folderA, File("root-a", "hit-root-a", RootKey)]),
            new FileBrowserPage(
                [folderB1, File("b-z", "hit-b-z", folderB.Key)],
                nextContinuationToken: "folder-b-page-2"),
            new FileBrowserPage(
                [File("b-a", "hit-b-a", folderB.Key)],
                warnings: [new FileBrowserPageWarning("folder-b-page-2", "Folder B page two warning.")]),
            new FileBrowserPage([File("a", "hit-a", folderA.Key)]),
            new FileBrowserPage([File("deep", "hit-deep", folderB1.Key)])
        };
        var provider = new RecordingProvider(CreateDescriptor(
            recommendedPageSize: 2,
            maximumPageSize: 3));
        var data = new RecordingSearchData(pages: pages);
        var sort = new FileBrowserSortDescriptor(
            FileBrowserSortField.Name,
            FileBrowserSortDirection.Ascending,
            FoldersFirst: false);
        var metadata = new FileBrowserMetadataRequest(
            FileBrowserMetadataFields.Name | FileBrowserMetadataFields.DisplayPath,
            IncludeExpensive: true);
        var request = SearchRequest(
            "hit",
            FileBrowserSearchScope.Progressive,
            pageSize: 20,
            sort: sort,
            consistencyToken: "tree-revision-3",
            metadata: metadata);
        using var cancellation = new CancellationTokenSource();

        FileBrowserSearchPage result = await new ProgressiveFileBrowserSearchStrategy().SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request),
            cancellation.Token);

        Assert.Equal("progressive-breadth-first", result.StrategyId);
        Assert.Equal(
            ["hit-a", "hit-b-a", "hit-b-z", "hit-deep", "hit-root-a", "hit-root-z"],
            Names(result));
        Assert.Null(result.NextContinuationToken);
        Assert.Equal(6L, result.TotalCount);
        Assert.False(result.IsPartial);
        Assert.Equal(4, result.ScannedContainers);
        Assert.Equal(9, result.ScannedItems);
        Assert.Equal("tree-revision-3", result.ConsistencyToken);
        Assert.Equal(["root-page-1", "folder-b-page-2"], result.Warnings.Select(warning => warning.Code));

        Assert.Collection(
            data.BrowseCalls,
            call => AssertBrowseCall(call, RootKey, null, FileBrowserPageApplyMode.Replace),
            call => AssertBrowseCall(call, RootKey, "root-page-2", FileBrowserPageApplyMode.Append),
            call => AssertBrowseCall(call, folderB.Key, null, FileBrowserPageApplyMode.Replace),
            call => AssertBrowseCall(call, folderB.Key, "folder-b-page-2", FileBrowserPageApplyMode.Append),
            call => AssertBrowseCall(call, folderA.Key, null, FileBrowserPageApplyMode.Replace),
            call => AssertBrowseCall(call, folderB1.Key, null, FileBrowserPageApplyMode.Replace));
        foreach (BrowseCall call in data.BrowseCalls)
        {
            Assert.Equal(2, call.Request.PageSize);
            Assert.Same(sort, call.Request.Sort);
            Assert.Same(FileBrowserFilter.None, call.Request.Filter);
            Assert.False(call.Request.IncludeDescendants);
            Assert.Equal(
                call.Request.ParentKey == RootKey ? "tree-revision-3" : null,
                call.Request.ConsistencyToken);
            Assert.Same(metadata, call.Request.Metadata);
            Assert.True(call.CancellationToken.CanBeCanceled);
        }

        Assert.Equal(0, data.GetLoadedDescendantsCallCount);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveSearchKeepsConsistencyTokensScopedToTheirContainer()
    {
        FileBrowserItem child = Container("child-with-own-revision", "Child");
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
                [child, File("root-first", "match-root-first.cs")],
                nextContinuationToken: "root-page-2",
                consistencyToken: "root-revision"),
            new FileBrowserPage(
                [File("root-second", "match-root-second.cs")],
                consistencyToken: "root-revision"),
            new FileBrowserPage(
                [File("child-first", "match-child-first.cs", child.Key)],
                nextContinuationToken: "child-page-2",
                consistencyToken: "child-revision"),
            new FileBrowserPage(
                [File("child-second", "match-child-second.cs", child.Key)],
                consistencyToken: "child-revision")
        ]);
        var provider = new RecordingProvider(CreateDescriptor());
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            pageSize: 10,
            consistencyToken: "root-revision");

        FileBrowserSearchPage result = await new ProgressiveFileBrowserSearchStrategy().SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));

        Assert.Equal(
            [
                "match-child-first.cs",
                "match-child-second.cs",
                "match-root-first.cs",
                "match-root-second.cs"
            ],
            Names(result));
        Assert.Equal(2, result.ScannedContainers);
        Assert.Equal(5, result.ScannedItems);
        Assert.False(result.IsPartial);
        Assert.Collection(
            data.BrowseCalls,
            call =>
            {
                AssertBrowseCall(call, RootKey, null, FileBrowserPageApplyMode.Replace);
                Assert.Equal("root-revision", call.Request.ConsistencyToken);
            },
            call =>
            {
                AssertBrowseCall(call, RootKey, "root-page-2", FileBrowserPageApplyMode.Append);
                Assert.Equal("root-revision", call.Request.ConsistencyToken);
            },
            call =>
            {
                AssertBrowseCall(call, child.Key, null, FileBrowserPageApplyMode.Replace);
                Assert.Null(call.Request.ConsistencyToken);
            },
            call =>
            {
                AssertBrowseCall(call, child.Key, "child-page-2", FileBrowserPageApplyMode.Append);
                Assert.Equal("child-revision", call.Request.ConsistencyToken);
            });
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveSearchDeduplicatesOverlappingPagesBeforeCountingAndFilters()
    {
        FileBrowserItem child = Container("child", "Child");
        FileBrowserItem shared = File("shared", "match-shared.cs", mediaType: "text/plain");
        FileBrowserItem pathMatch = File(
            "path-match",
            "path-only.cs",
            displayPath: "/root/MATCH/path-only.cs",
            mediaType: "text/plain");
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
                [
                    child,
                    shared,
                    File("wrong-extension", "match-notes.md", mediaType: "text/plain"),
                    File("no-query-match", "ordinary.cs", mediaType: "text/plain")
                ],
                nextContinuationToken: "overlap"),
            new FileBrowserPage([shared, pathMatch]),
            new FileBrowserPage(
            [
                File("child-match", "child-match.cs", child.Key, mediaType: "text/x-csharp"),
                File("wrong-media", "match-data.cs", child.Key, mediaType: "application/json")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor(recommendedPageSize: 10, maximumPageSize: 10));
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            pageSize: 10,
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.Name,
                FileBrowserSortDirection.Ascending,
                FoldersFirst: false),
            filter: new FileBrowserFilter(
                kinds: [FileBrowserItemKind.File],
                categories: [FileBrowserItemCategory.Code],
                extensions: [".cs"],
                mediaTypePrefix: "text/"));

        FileBrowserSearchPage result = await new ProgressiveFileBrowserSearchStrategy().SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));

        Assert.Equal(["child-match.cs", "match-shared.cs", "path-only.cs"], Names(result));
        Assert.Equal(3L, result.TotalCount);
        Assert.Null(result.NextContinuationToken);
        Assert.False(result.IsPartial);
        Assert.Equal(2, result.ScannedContainers);
        Assert.Equal(7, result.ScannedItems);
        Assert.Empty(result.Warnings);
        Assert.Collection(
            data.BrowseCalls,
            call => AssertBrowseCall(call, RootKey, null, FileBrowserPageApplyMode.Replace),
            call => AssertBrowseCall(call, RootKey, "overlap", FileBrowserPageApplyMode.Append),
            call => AssertBrowseCall(call, child.Key, null, FileBrowserPageApplyMode.Replace));
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveSearchStopsAtItemBudgetWithoutLoadingMoreOrDescending()
    {
        FileBrowserItem child = Container("budget-child", "Budget child");
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
                [
                    child,
                    File("within-budget", "match-within-budget.cs"),
                    File("past-budget", "match-past-budget.cs")
                ],
                nextContinuationToken: "unrequested-page")
        ]);
        var provider = new RecordingProvider(CreateDescriptor(recommendedPageSize: 10, maximumPageSize: 10));
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            pageSize: 10,
            budget: new FileBrowserSearchBudget(maximumContainers: 10, maximumItems: 2));

        FileBrowserSearchPage result = await new ProgressiveFileBrowserSearchStrategy().SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));

        Assert.Equal(["match-within-budget.cs"], Names(result));
        Assert.Equal(1L, result.TotalCount);
        Assert.Null(result.NextContinuationToken);
        Assert.True(result.IsPartial);
        Assert.Equal(1, result.ScannedContainers);
        Assert.Equal(2, result.ScannedItems);
        FileBrowserPageWarning warning = Assert.Single(result.Warnings);
        Assert.Equal("search-budget-reached", warning.Code);
        Assert.Equal(
            "Search stopped after 1 containers or 2 items. Results are partial.",
            warning.Message);
        BrowseCall call = Assert.Single(data.BrowseCalls);
        AssertBrowseCall(call, RootKey, null, FileBrowserPageApplyMode.Replace);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveSearchStopsAtMatchBudgetAndReportsRetainedState()
    {
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                File("one", "match-one.cs"),
                File("two", "match-two.cs"),
                File("three", "match-three.cs")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor(recommendedPageSize: 10, maximumPageSize: 10));
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            pageSize: 10,
            budget: new FileBrowserSearchBudget(
                maximumContainers: 10,
                maximumItems: 100,
                maximumMatches: 2,
                maximumRetainedBytes: 64 * 1024));

        FileBrowserSearchPage result = await new ProgressiveFileBrowserSearchStrategy().SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));

        Assert.Equal(["match-one.cs", "match-two.cs"], Names(result));
        Assert.True(result.IsPartial);
        Assert.Equal(2, result.ScannedItems);
        Assert.Equal(2, result.RetainedItems);
        Assert.InRange(result.RetainedBytes, 1, request.Budget.MaximumRetainedBytes);
        Assert.Equal(1, result.PeakConcurrentRequests);
        Assert.Contains(result.Warnings, warning => warning.Code == "search-budget-reached");
    }

    [Fact]
    public async Task ProgressiveSearchStopsBeforeRetainedByteBudgetIsExceeded()
    {
        FileBrowserItem first = File("one", $"match-{new string('a', 256)}.cs");
        long firstBytes = FileBrowserSearchRetentionMeasure.Measure(first);
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                first,
                File("two", $"match-{new string('b', 256)}.cs")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor(recommendedPageSize: 10, maximumPageSize: 10));
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            pageSize: 10,
            budget: new FileBrowserSearchBudget(
                maximumContainers: 10,
                maximumItems: 100,
                maximumMatches: 10,
                maximumRetainedBytes: firstBytes));

        FileBrowserSearchPage result = await new ProgressiveFileBrowserSearchStrategy().SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));

        Assert.Equal([first.Name], Names(result));
        Assert.True(result.IsPartial);
        Assert.Equal(1, result.RetainedItems);
        Assert.Equal(firstBytes, result.RetainedBytes);
        Assert.Contains(result.Warnings, warning => warning.Code == "search-budget-reached");
    }

    [Fact]
    public async Task ProgressiveSearchDurationBudgetCancelsInFlightBrowseAndReturnsPartialState()
    {
        var data = new RecordingSearchData
        {
            BrowseHandler = async (_, _, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                return new FileBrowserPage([]);
            }
        };
        var provider = new RecordingProvider(CreateDescriptor());
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            budget: new FileBrowserSearchBudget(
                maximumContainers: 10,
                maximumItems: 100,
                maximumDuration: TimeSpan.FromMilliseconds(50)));

        FileBrowserSearchPage result = await new ProgressiveFileBrowserSearchStrategy().SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));

        Assert.True(result.IsPartial);
        Assert.Empty(result.Items);
        Assert.Equal(1, result.ScannedContainers);
        Assert.Equal(0, result.ScannedItems);
        Assert.True(result.Elapsed >= TimeSpan.FromMilliseconds(40));
        Assert.Contains(result.Warnings, warning => warning.Code == "search-budget-reached");
    }

    [Fact]
    public async Task ProgressiveSearchStopsBeforeExceedingContainerBudgetAndReportsActualScans()
    {
        FileBrowserItem child = Container("container-budget-child", "Container budget child");
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                child,
                File("root-budget-match", "match-at-root.cs")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor(recommendedPageSize: 10, maximumPageSize: 10));
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            pageSize: 10,
            budget: new FileBrowserSearchBudget(maximumContainers: 1, maximumItems: 100));

        FileBrowserSearchPage result = await new ProgressiveFileBrowserSearchStrategy().SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));

        Assert.Equal(["match-at-root.cs"], Names(result));
        Assert.Equal(1L, result.TotalCount);
        Assert.Null(result.NextContinuationToken);
        Assert.True(result.IsPartial);
        Assert.Equal(1, result.ScannedContainers);
        Assert.Equal(2, result.ScannedItems);
        FileBrowserPageWarning warning = Assert.Single(result.Warnings);
        Assert.Equal("search-budget-reached", warning.Code);
        Assert.Equal(
            "Search stopped after 1 containers or 2 items. Results are partial.",
            warning.Message);
        BrowseCall call = Assert.Single(data.BrowseCalls);
        AssertBrowseCall(call, RootKey, null, FileBrowserPageApplyMode.Replace);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveContinuationSurvivesTraversalBeyondLoadedTreeBudgetAndCompleteEviction()
    {
        FileBrowserItem folder = Container("snapshot-folder", "Snapshot folder");
        var data = new RecordingSearchData(
            loadedDescendants: [File("evictable", "match-evictable.cs")],
            pages:
            [
                new FileBrowserPage(
                [
                    folder,
                    File("e", "match-e.cs"),
                    File("a", "match-a.cs")
                ]),
                new FileBrowserPage(
                [
                    File("d", "match-d.cs", folder.Key),
                    File("b", "match-b.cs", folder.Key),
                    File("c", "match-c.cs", folder.Key),
                    File("excluded", "ordinary.cs", folder.Key)
                ])
            ]);
        var provider = new RecordingProvider(CreateDescriptor(recommendedPageSize: 4, maximumPageSize: 4));
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            pageSize: 2,
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.Name,
                FileBrowserSortDirection.Ascending,
                FoldersFirst: false),
            consistencyToken: "cached-revision");
        var strategy = new ProgressiveFileBrowserSearchStrategy();

        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));

        Assert.Equal(["match-a.cs", "match-b.cs"], Names(first));
        Assert.NotNull(first.NextContinuationToken);
        Assert.StartsWith("pfs1.", first.NextContinuationToken, StringComparison.Ordinal);
        Assert.DoesNotContain("offset", first.NextContinuationToken, StringComparison.OrdinalIgnoreCase);

        // Simulate an aggressively budgeted tree cache evicting every page visited by the traversal.
        data.ReplaceLoadedDescendants([]);

        FileBrowserSearchPage second = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(
                provider,
                data,
                request.Next(first.NextContinuationToken!, first.ConsistencyToken)));

        Assert.Equal("progressive-breadth-first", second.StrategyId);
        Assert.Equal(["match-c.cs", "match-d.cs"], Names(second));
        Assert.NotEqual(first.NextContinuationToken, second.NextContinuationToken);
        Assert.Equal(5L, second.TotalCount);
        Assert.False(second.IsPartial);
        Assert.Equal(2, second.ScannedContainers);
        Assert.Equal(7, second.ScannedItems);
        Assert.Equal("cached-revision", second.ConsistencyToken);
        Assert.Empty(second.Warnings);
        Assert.Equal(0, data.GetLoadedDescendantsCallCount);
        Assert.Equal(2, data.BrowseCalls.Count);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveContinuationUsesCapturedOrderingAfterLoadedSourceMutates()
    {
        var data = new RecordingSearchData(
            pages:
            [
                new FileBrowserPage(
                [
                    File("d-stable", "match-d.cs"),
                    File("a-stable", "match-a.cs"),
                    File("c-stable", "match-c.cs"),
                    File("b-stable", "match-b.cs")
                ])
            ]);
        var provider = new RecordingProvider(CreateDescriptor(recommendedPageSize: 4, maximumPageSize: 4));
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            pageSize: 2);
        var strategy = new ProgressiveFileBrowserSearchStrategy();

        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));

        data.ReplaceLoadedDescendants(
        [
            File("new-aa", "match-aa-new.cs"),
            File("new-z", "match-z-new.cs")
        ]);

        FileBrowserSearchPage second = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(
                provider,
                data,
                request.Next(first.NextContinuationToken!, first.ConsistencyToken)));

        Assert.Equal(["match-a.cs", "match-b.cs"], Names(first));
        Assert.Equal(["match-c.cs", "match-d.cs"], Names(second));
        Assert.Null(second.NextContinuationToken);
        Assert.Single(data.BrowseCalls);
        Assert.Equal(0, data.GetLoadedDescendantsCallCount);
    }

    [Fact]
    public async Task ProgressiveContinuationRejectsCrossQueryTokenReuseWithoutConsumingOriginalCursor()
    {
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                File("one", "match-one.cs"),
                File("two", "match-two.cs")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor());
        var strategy = new ProgressiveFileBrowserSearchStrategy();
        var request = SearchRequest("match", FileBrowserSearchScope.Progressive, pageSize: 1);
        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));
        var mismatched = SearchRequest(
            "other-query",
            FileBrowserSearchScope.Progressive,
            pageSize: 1,
            continuationToken: first.NextContinuationToken,
            consistencyToken: first.ConsistencyToken);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => strategy.SearchAsync(
                new FileBrowserSearchStrategyContext(provider, data, mismatched)).AsTask());

        AssertStaleCursor(exception);
        FileBrowserSearchPage valid = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(
                provider,
                data,
                request.Next(first.NextContinuationToken!, first.ConsistencyToken)));
        Assert.Equal(["match-two.cs"], Names(valid));
    }

    [Fact]
    public async Task ProgressiveContinuationRejectsRevisionMismatchAndAcceptsCapturedRootRevision()
    {
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                File("one", "match-one.cs"),
                File("two", "match-two.cs")
            ],
            consistencyToken: "root-revision-2")
        ]);
        var provider = new RecordingProvider(CreateDescriptor());
        var strategy = new ProgressiveFileBrowserSearchStrategy();
        var request = SearchRequest(
            "match",
            FileBrowserSearchScope.Progressive,
            pageSize: 1);
        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));
        Assert.Equal("root-revision-2", first.ConsistencyToken);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => strategy.SearchAsync(
                new FileBrowserSearchStrategyContext(
                    provider,
                    data,
                    request.Next(first.NextContinuationToken!, "root-revision-1"))).AsTask());

        AssertStaleCursor(exception);
        FileBrowserSearchPage valid = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(
                provider,
                data,
                request.Next(first.NextContinuationToken!, first.ConsistencyToken)));
        Assert.Equal(["match-two.cs"], Names(valid));
    }

    [Fact]
    public async Task ProgressiveContinuationLruEvictionReturnsRetryableStaleCursor()
    {
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                File("first-a", "first-a.cs"),
                File("first-b", "first-b.cs")
            ]),
            new FileBrowserPage(
            [
                File("second-a", "second-a.cs"),
                File("second-b", "second-b.cs")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor());
        var strategy = new ProgressiveFileBrowserSearchStrategy(maximumRetainedSearches: 1);
        var firstRequest = SearchRequest("first", FileBrowserSearchScope.Progressive, pageSize: 1);
        var secondRequest = SearchRequest("second", FileBrowserSearchScope.Progressive, pageSize: 1);
        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, firstRequest));
        FileBrowserSearchPage second = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, secondRequest));
        Assert.NotEqual(first.NextContinuationToken, second.NextContinuationToken);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => strategy.SearchAsync(
                new FileBrowserSearchStrategyContext(
                    provider,
                    data,
                    firstRequest.Next(first.NextContinuationToken!, first.ConsistencyToken))).AsTask());

        AssertStaleCursor(exception);
    }

    [Fact]
    public async Task ProgressiveContinuationExpiresAfterFiniteRetention()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero));
        var strategy = new ProgressiveFileBrowserSearchStrategy(
            retention: TimeSpan.FromMinutes(1),
            timeProvider: clock);
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                File("one", "match-one.cs"),
                File("two", "match-two.cs")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor());
        var request = SearchRequest("match", FileBrowserSearchScope.Progressive, pageSize: 1);
        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));
        clock.Advance(TimeSpan.FromMinutes(1));

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => strategy.SearchAsync(
                new FileBrowserSearchStrategyContext(
                    provider,
                    data,
                    request.Next(first.NextContinuationToken!, first.ConsistencyToken))).AsTask());

        AssertStaleCursor(exception);
    }

    [Fact]
    public async Task ProgressiveFinalContinuationRemainsIdempotentForSafeRetry()
    {
        var strategy = new ProgressiveFileBrowserSearchStrategy();
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                File("one", "match-one.cs"),
                File("two", "match-two.cs")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor());
        var request = SearchRequest("match", FileBrowserSearchScope.Progressive, pageSize: 1);
        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));
        FileBrowserSearchRequest continuationRequest = request.Next(
            first.NextContinuationToken!,
            first.ConsistencyToken);
        FileBrowserSearchPage final = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, continuationRequest));
        Assert.Null(final.NextContinuationToken);

        FileBrowserSearchPage retried = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, continuationRequest));

        Assert.Equal(Names(final), Names(retried));
        Assert.Null(retried.NextContinuationToken);
    }

    [Fact]
    public async Task ProgressiveContinuationCancellationDoesNotAdvanceRetainedCheckpoint()
    {
        var strategy = new ProgressiveFileBrowserSearchStrategy();
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                File("one", "match-one.cs"),
                File("two", "match-two.cs")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor());
        var request = SearchRequest("match", FileBrowserSearchScope.Progressive, pageSize: 1);
        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));
        FileBrowserSearchRequest continuationRequest = request.Next(
            first.NextContinuationToken!,
            first.ConsistencyToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => strategy.SearchAsync(
                new FileBrowserSearchStrategyContext(provider, data, continuationRequest),
                cancellation.Token).AsTask());

        FileBrowserSearchPage second = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, continuationRequest));
        Assert.Equal(["match-two.cs"], Names(second));
        Assert.Null(second.NextContinuationToken);
        Assert.Equal(0, data.GetLoadedDescendantsCallCount);
        Assert.Single(data.BrowseCalls);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveContinuationTokenIsIdempotentAcrossAAbortedSessionCommit()
    {
        var strategy = new ProgressiveFileBrowserSearchStrategy();
        var data = new RecordingSearchData(pages:
        [
            new FileBrowserPage(
            [
                File("one", "match-one.cs"),
                File("two", "match-two.cs"),
                File("three", "match-three.cs"),
                File("four", "match-four.cs")
            ])
        ]);
        var provider = new RecordingProvider(CreateDescriptor(recommendedPageSize: 4, maximumPageSize: 4));
        var request = SearchRequest("match", FileBrowserSearchScope.Progressive, pageSize: 1);
        FileBrowserSearchPage first = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, request));
        FileBrowserSearchRequest secondRequest = request.Next(
            first.NextContinuationToken!,
            first.ConsistencyToken);

        FileBrowserSearchPage second = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, secondRequest));
        // Model a canceled/failed session publication by retrying its unchanged request token.
        FileBrowserSearchPage retriedSecond = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(provider, data, secondRequest));
        FileBrowserSearchPage third = await strategy.SearchAsync(
            new FileBrowserSearchStrategyContext(
                provider,
                data,
                request.Next(second.NextContinuationToken!, second.ConsistencyToken)));

        Assert.Equal(Names(second), Names(retriedSecond));
        Assert.Equal(second.NextContinuationToken, retriedSecond.NextContinuationToken);
        Assert.Equal(["match-three.cs"], Names(third));
        Assert.Single(data.BrowseCalls);
    }

    [Fact]
    public async Task ProgressiveTraversalRejectsARepeatedProviderCursorInsteadOfLooping()
    {
        var data = new RecordingSearchData
        {
            BrowseHandler = (request, _, _) => ValueTask.FromResult(request.ContinuationToken switch
            {
                null => new FileBrowserPage(
                    [File("one", "match-one.cs")],
                    nextContinuationToken: "cursor-a"),
                "cursor-a" => new FileBrowserPage(
                    [File("two", "match-two.cs")],
                    nextContinuationToken: "cursor-b"),
                "cursor-b" => new FileBrowserPage(
                    [File("three", "match-three.cs")],
                    nextContinuationToken: "cursor-a"),
                _ => throw new InvalidOperationException("Unexpected cursor.")
            })
        };
        var provider = new RecordingProvider(CreateDescriptor(recommendedPageSize: 1, maximumPageSize: 1));
        var request = SearchRequest("match", FileBrowserSearchScope.Progressive, pageSize: 10);

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => new ProgressiveFileBrowserSearchStrategy().SearchAsync(
                new FileBrowserSearchStrategyContext(provider, data, request)).AsTask());

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, exception.Error.Code);
        Assert.Equal(3, data.BrowseCalls.Count);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveSearchHonorsPreCancellationBeforeBrowsing()
    {
        var data = new RecordingSearchData(pages: [new FileBrowserPage([])]);
        var provider = new RecordingProvider(CreateDescriptor());
        var request = SearchRequest("match", FileBrowserSearchScope.Progressive);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ProgressiveFileBrowserSearchStrategy().SearchAsync(
                new FileBrowserSearchStrategyContext(provider, data, request),
                cancellation.Token).AsTask());

        Assert.Empty(data.BrowseCalls);
        Assert.Equal(0, data.GetLoadedDescendantsCallCount);
        AssertNoProviderIo(provider);
    }

    [Fact]
    public async Task ProgressiveSearchObservesCancellationBetweenProviderPages()
    {
        using var cancellation = new CancellationTokenSource();
        var data = new RecordingSearchData
        {
            BrowseHandler = (_, _, _) =>
            {
                cancellation.Cancel();
                return ValueTask.FromResult(new FileBrowserPage(
                    [File("first-page-item", "match-first-page.cs")],
                    nextContinuationToken: "page-that-must-not-load"));
            }
        };
        var provider = new RecordingProvider(CreateDescriptor());
        var request = SearchRequest("match", FileBrowserSearchScope.Progressive);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ProgressiveFileBrowserSearchStrategy().SearchAsync(
                new FileBrowserSearchStrategyContext(provider, data, request),
                cancellation.Token).AsTask());

        BrowseCall call = Assert.Single(data.BrowseCalls);
        AssertBrowseCall(call, RootKey, null, FileBrowserPageApplyMode.Replace);
        Assert.True(call.CancellationToken.CanBeCanceled);
        AssertNoProviderIo(provider);
    }

    private static FileBrowserSearchRequest SearchRequest(
        string query,
        FileBrowserSearchScope scope,
        int pageSize = 50,
        string? continuationToken = null,
        FileBrowserSortDescriptor? sort = null,
        FileBrowserFilter? filter = null,
        FileBrowserSearchBudget? budget = null,
        string? consistencyToken = null,
        FileBrowserMetadataRequest? metadata = null)
        => new(
            RootKey,
            query,
            scope,
            pageSize,
            continuationToken,
            sort,
            filter,
            budget,
            consistencyToken,
            metadata);

    private static FileBrowserSourceDescriptor CreateDescriptor(
        FileBrowserSourceCapabilities capabilities = FileBrowserSourceCapabilities.PagedBrowse,
        IEnumerable<FileBrowserSearchScope>? searchScopes = null,
        int recommendedPageSize = 2,
        int maximumPageSize = 3)
        => new(
            SourceId,
            "Search test source",
            capabilities: capabilities,
            recommendedPageSize: recommendedPageSize,
            maximumPageSize: maximumPageSize,
            supportedSearchScopes: searchScopes ?? Enum.GetValues<FileBrowserSearchScope>());

    private static FileBrowserItem File(
        string id,
        string name,
        FileBrowserItemKey? parentKey = null,
        FileBrowserItemCategory category = FileBrowserItemCategory.Code,
        string? displayPath = null,
        long? size = null,
        string? mediaType = null,
        DateTimeOffset? modifiedAt = null,
        string? contentIdentity = null)
        => new(
            Key(id),
            parentKey ?? RootKey,
            name,
            FileBrowserItemKind.File,
            category,
            displayPath: displayPath,
            size: size,
            mediaType: mediaType,
            modifiedAt: modifiedAt,
            contentIdentity: contentIdentity is null
                ? null
                : new FileBrowserContentIdentity("sha256", contentIdentity));

    private static FileBrowserItem Container(
        string id,
        string name,
        FileBrowserItemKey? parentKey = null)
        => new(
            Key(id),
            parentKey ?? RootKey,
            name,
            FileBrowserItemKind.Container,
            FileBrowserItemCategory.Folder,
            childState: FileBrowserChildState.HasChildren);

    private static FileBrowserItemKey Key(string value) => new(SourceId, value);

    private static string[] Names(FileBrowserSearchPage page)
        => page.Items.Select(item => item.Name).ToArray();

    private static void AssertBrowseCall(
        BrowseCall call,
        FileBrowserItemKey expectedParent,
        string? expectedContinuationToken,
        FileBrowserPageApplyMode expectedMode)
    {
        Assert.Equal(expectedParent, call.Request.ParentKey);
        Assert.Equal(expectedContinuationToken, call.Request.ContinuationToken);
        Assert.Equal(expectedMode, call.ApplyMode);
    }

    private static void AssertNoProviderIo(RecordingProvider provider)
    {
        Assert.Equal(0, provider.GetRootCallCount);
        Assert.Equal(0, provider.GetPathCallCount);
        Assert.Equal(0, provider.BrowseCallCount);
    }

    private static void AssertStaleCursor(FileBrowserProviderException exception)
    {
        Assert.Equal(FileBrowserErrorCode.StaleCursor, exception.Error.Code);
        Assert.True(exception.Error.IsRetryable);
        Assert.Equal(
            "The progressive-search continuation token is missing, expired, or does not match this search.",
            exception.Error.Message);
    }

    private sealed class RecordingSearchData : IFileBrowserSearchData
    {
        private readonly Queue<FileBrowserPage> pages;
        private IReadOnlyList<FileBrowserItem> loadedDescendants;

        public RecordingSearchData(
            IEnumerable<FileBrowserItem>? currentItems = null,
            IEnumerable<FileBrowserItem>? loadedDescendants = null,
            IEnumerable<FileBrowserPage>? pages = null)
        {
            CurrentItems = (currentItems ?? []).ToArray();
            this.loadedDescendants = (loadedDescendants ?? []).ToArray();
            this.pages = new Queue<FileBrowserPage>(pages ?? []);
        }

        public IReadOnlyList<FileBrowserItem> CurrentItems { get; }

        public List<BrowseCall> BrowseCalls { get; } = [];

        public int GetLoadedDescendantsCallCount { get; private set; }

        public void ReplaceLoadedDescendants(IEnumerable<FileBrowserItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            loadedDescendants = items.ToArray();
        }

        public Func<FileBrowserBrowseRequest, FileBrowserPageApplyMode, CancellationToken, ValueTask<FileBrowserPage>>?
            BrowseHandler
        { get; init; }

        public ValueTask<FileBrowserPage> BrowseAndCacheAsync(
            FileBrowserBrowseRequest request,
            FileBrowserPageApplyMode applyMode,
            CancellationToken cancellationToken = default)
        {
            BrowseCalls.Add(new BrowseCall(request, applyMode, cancellationToken));
            if (BrowseHandler is not null)
            {
                return BrowseHandler(request, applyMode, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (pages.Count == 0)
            {
                throw new InvalidOperationException("The strategy requested an unexpected browse page.");
            }

            return ValueTask.FromResult(pages.Dequeue());
        }

        public bool TryGetItem(FileBrowserItemKey key, out FileBrowserItem? item)
        {
            item = CurrentItems.Concat(loadedDescendants).FirstOrDefault(candidate => candidate.Key == key);
            return item is not null;
        }

        public IReadOnlyList<FileBrowserItem> GetLoadedChildren(FileBrowserItemKey parentKey)
            => loadedDescendants.Where(item => item.ParentKey == parentKey).ToArray();

        public IReadOnlyList<FileBrowserItem> GetLoadedDescendants(FileBrowserItemKey parentKey)
        {
            GetLoadedDescendantsCallCount++;
            return loadedDescendants;
        }
    }

    private class RecordingProvider : IFileBrowserProvider
    {
        public RecordingProvider(FileBrowserSourceDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public FileBrowserSourceDescriptor Descriptor { get; }

        public int GetRootCallCount { get; private set; }

        public int GetPathCallCount { get; private set; }

        public int BrowseCallCount { get; private set; }

        public ValueTask<FileBrowserItem> GetRootAsync(
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
        {
            GetRootCallCount++;
            throw new InvalidOperationException("The search strategy must not request the provider root.");
        }

        public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
            FileBrowserItemKey itemKey,
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
        {
            GetPathCallCount++;
            throw new InvalidOperationException("The search strategy must not request a provider path.");
        }

        public ValueTask<FileBrowserPage> BrowseAsync(
            FileBrowserBrowseRequest request,
            CancellationToken cancellationToken = default)
        {
            BrowseCallCount++;
            throw new InvalidOperationException("Search strategies browse through the search-data boundary.");
        }
    }

    private sealed class NativeRecordingProvider : RecordingProvider, IFileBrowserSearchProvider
    {
        private readonly Func<
            FileBrowserSearchRequest,
            CancellationToken,
            ValueTask<FileBrowserSearchPage>> handler;

        public NativeRecordingProvider(
            FileBrowserSourceDescriptor descriptor,
            Func<
                FileBrowserSearchRequest,
                CancellationToken,
                ValueTask<FileBrowserSearchPage>>? handler = null)
            : base(descriptor)
        {
            this.handler = handler ?? ((_, _) => throw new InvalidOperationException(
                "The native search provider received an unexpected search call."));
        }

        public List<SearchCall> SearchCalls { get; } = [];

        public ValueTask<FileBrowserSearchPage> SearchAsync(
            FileBrowserSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            SearchCalls.Add(new SearchCall(request, cancellationToken));
            return handler(request, cancellationToken);
        }
    }

    private sealed record BrowseCall(
        FileBrowserBrowseRequest Request,
        FileBrowserPageApplyMode ApplyMode,
        CancellationToken CancellationToken);

    private sealed record SearchCall(
        FileBrowserSearchRequest Request,
        CancellationToken CancellationToken);

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan elapsed) => utcNow += elapsed;
    }
}

