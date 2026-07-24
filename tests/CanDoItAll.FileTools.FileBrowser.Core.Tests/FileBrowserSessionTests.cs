namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class FileBrowserSessionTests
{
    [Fact]
    public async Task InitializeLoadsOnlyRootFolderAndPublishesInitializingThenIdle()
    {
        var root = TestFileBrowserFactory.Container("root", revision: "revision-1");
        var folder = TestFileBrowserFactory.Container("folder", root.Key);
        var file = TestFileBrowserFactory.File("readme", root.Key, "README.md");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(maximumPageSize: 2))
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage(
                [folder, file],
                nextContinuationToken: "page-2",
                totalCount: 3,
                consistencyToken: "revision-1"))
        };
        await using var session = new FileBrowserSession(
            [provider],
            new FileBrowserSessionOptions(pageSize: 50));
        var operations = new List<FileBrowserOperationKind>();
        session.Changed += (_, args) => operations.Add(args.Snapshot.Operation);

        await session.InitializeAsync();

        Assert.Equal(1, provider.RootCallCount);
        Assert.Equal(0, provider.PathCallCount);
        Assert.Equal(1, provider.BrowseCallCount);
        var request = Assert.Single(provider.BrowseCalls);
        Assert.Equal(root.Key, request.ParentKey);
        Assert.Equal(2, request.PageSize);
        Assert.Equal("revision-1", request.ConsistencyToken);
        Assert.False(request.IncludeDescendants);
        Assert.Equal([FileBrowserOperationKind.Initializing, FileBrowserOperationKind.Idle], operations);

        var snapshot = session.Snapshot;
        Assert.Equal(provider.Descriptor, snapshot.CurrentSource);
        Assert.Same(root, snapshot.CurrentContainer);
        Assert.Equal([folder.Key, file.Key], snapshot.Items.Select(item => item.Key));
        Assert.True(snapshot.HasMore);
        Assert.Equal("page-2", snapshot.NextContinuationToken);
        Assert.Equal(3, snapshot.TotalCount);
        Assert.False(snapshot.IsBusy);
        Assert.Null(snapshot.Error);
        Assert.False(snapshot.CanGoBack);
        Assert.False(snapshot.CanGoForward);
        Assert.False(snapshot.CanGoUp);
    }

    [Fact]
    public async Task InitializeAtOccurrenceResolvesItsPathWithoutLoadingRoot()
    {
        var root = TestFileBrowserFactory.Container("root");
        var folder = TestFileBrowserFactory.Container("folder", root.Key);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            PathHandler = (key, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root, folder]),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([
                TestFileBrowserFactory.File("child", folder.Key)
            ]))
        };
        await using var session = new FileBrowserSession([provider]);

        await session.InitializeAsync(provider.Descriptor.Id, folder.Key);

        Assert.Equal(0, provider.RootCallCount);
        Assert.Equal(1, provider.PathCallCount);
        Assert.Equal([folder.Key], provider.PathCalls);
        Assert.Equal(1, provider.BrowseCallCount);
        Assert.Equal(folder.Key, session.Snapshot.CurrentContainer!.Key);
        Assert.True(session.Snapshot.CanGoUp);
        Assert.Equal("child", Assert.Single(session.Snapshot.Items).Key.Value);

        Assert.Throws<ArgumentException>(() => session.InitializeAsync(
            provider.Descriptor.Id,
            TestFileBrowserFactory.Key("foreign", "other")));
    }

    [Fact]
    public async Task ChangeSourceResetsHistorySelectionAndReusesPriorSourceCache()
    {
        var firstRoot = TestFileBrowserFactory.Container("root", source: "first");
        var firstItem = TestFileBrowserFactory.File("first-item", firstRoot.Key, source: "first");
        var first = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("first", "First"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(firstRoot),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([firstItem]))
        };
        var secondRoot = TestFileBrowserFactory.Container("root", source: "second");
        var secondItem = TestFileBrowserFactory.File("second-item", secondRoot.Key, source: "second");
        var second = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("second", "Second"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(secondRoot),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([secondItem]))
        };
        await using var session = new FileBrowserSession([first, second]);
        await session.InitializeAsync(first.Descriptor.Id);
        session.Select(firstItem.Key);

        await session.ChangeSourceAsync(second.Descriptor.Id);

        Assert.Equal(second.Descriptor, session.Snapshot.CurrentSource);
        Assert.Equal(secondRoot.Key, session.Snapshot.CurrentContainer!.Key);
        Assert.Equal([secondItem.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Empty(session.Snapshot.SelectedKeys);
        Assert.False(session.Snapshot.CanGoBack);
        Assert.False(session.Snapshot.CanGoForward);
        Assert.Equal(1, first.BrowseCallCount);
        Assert.Equal(1, second.BrowseCallCount);

        await session.ChangeSourceAsync(first.Descriptor.Id);

        Assert.Equal(first.Descriptor, session.Snapshot.CurrentSource);
        Assert.Equal([firstItem.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Equal(2, first.RootCallCount);
        Assert.Equal(1, first.BrowseCallCount);
    }

    [Fact]
    public async Task BackForwardAndUpReuseCachedFolderPages()
    {
        var root = TestFileBrowserFactory.Container("root");
        var folder = TestFileBrowserFactory.Container("folder", root.Key);
        var nested = TestFileBrowserFactory.Container("nested", folder.Key);
        var leaf = TestFileBrowserFactory.File("leaf", nested.Key);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            PathHandler = (key, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>(
                key == folder.Key ? [root, folder] : [root, folder, nested]),
            BrowseHandler = (request, _) => ValueTask.FromResult(
                request.ParentKey == root.Key
                    ? new FileBrowserPage([folder])
                    : request.ParentKey == folder.Key
                        ? new FileBrowserPage([nested])
                        : new FileBrowserPage([leaf]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        session.Select(folder.Key);
        await session.NavigateAsync(folder.Key);
        Assert.Empty(session.Snapshot.SelectedKeys);
        await session.NavigateAsync(nested.Key);

        Assert.Equal(3, provider.BrowseCallCount);
        Assert.Equal(2, provider.PathCallCount);
        Assert.Equal(nested.Key, session.Snapshot.CurrentContainer!.Key);

        await session.GoBackAsync();
        Assert.Equal(folder.Key, session.Snapshot.CurrentContainer!.Key);
        Assert.True(session.Snapshot.CanGoForward);
        Assert.True(session.Snapshot.CanGoUp);

        await session.GoForwardAsync();
        Assert.Equal(nested.Key, session.Snapshot.CurrentContainer!.Key);
        Assert.False(session.Snapshot.CanGoForward);

        await session.GoUpAsync();
        Assert.Equal(folder.Key, session.Snapshot.CurrentContainer!.Key);
        Assert.True(session.Snapshot.CanGoBack);
        Assert.False(session.Snapshot.CanGoForward);
        Assert.Equal(3, provider.BrowseCallCount);
        Assert.Equal([root.Key, folder.Key, nested.Key], provider.BrowseCalls.Select(call => call.ParentKey));
    }

    [Fact]
    public async Task QueryChangesLoadOnceAndReturningToCachedQueryAvoidsProviderCalls()
    {
        var root = TestFileBrowserFactory.Container("root");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) => ValueTask.FromResult(new FileBrowserPage([
                TestFileBrowserFactory.File(
                    $"item-{request.Sort.Field}-{request.Filter.Extensions.Count}",
                    root.Key)
            ]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        await session.SetSortAsync(new FileBrowserSortDescriptor(FileBrowserSortField.Size));
        await session.SetSortAsync(new FileBrowserSortDescriptor());
        var codeFilter = new FileBrowserFilter(extensions: [".cs"]);
        await session.SetFilterAsync(codeFilter);
        await session.SetFilterAsync(codeFilter);

        Assert.Equal(3, provider.BrowseCallCount);
        Assert.Equal(FileBrowserSortField.Name, provider.BrowseCalls[0].Sort.Field);
        Assert.Equal(FileBrowserSortField.Size, provider.BrowseCalls[1].Sort.Field);
        Assert.Equal([".cs"], provider.BrowseCalls[2].Filter.Extensions);
        Assert.Equal(codeFilter, session.Snapshot.Filter);
        Assert.Equal("item-Name-1", Assert.Single(session.Snapshot.Items).Key.Value);
    }

    [Fact]
    public async Task BrowseLoadMoreDeduplicatesOverlapUpdatesMetadataAndPreservesSelection()
    {
        var root = TestFileBrowserFactory.Container("root");
        var alpha = TestFileBrowserFactory.File("alpha", root.Key, "Alpha");
        var betaOld = TestFileBrowserFactory.File("beta", root.Key, "Beta old", size: 10);
        var betaCurrent = TestFileBrowserFactory.File("beta", root.Key, "Beta current", size: 20);
        var gamma = TestFileBrowserFactory.File("gamma", root.Key, "Gamma");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) => ValueTask.FromResult(
                request.ContinuationToken is null
                    ? new FileBrowserPage(
                        [alpha, betaOld],
                        nextContinuationToken: "page-2",
                        totalCount: 3,
                        consistencyToken: "revision-1")
                    : new FileBrowserPage(
                        [betaCurrent, gamma],
                        totalCount: 3,
                        consistencyToken: "revision-1"))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        session.Select(betaOld.Key);

        await session.LoadMoreAsync();
        await session.LoadMoreAsync();

        Assert.Equal(2, provider.BrowseCallCount);
        var nextRequest = provider.BrowseCalls[1];
        Assert.Equal("page-2", nextRequest.ContinuationToken);
        Assert.Equal("revision-1", nextRequest.ConsistencyToken);
        Assert.Equal(["alpha", "beta", "gamma"], session.Snapshot.Items.Select(item => item.Key.Value));
        var beta = Assert.Single(session.Snapshot.Items, item => item.Key == betaOld.Key);
        Assert.Equal("Beta current", beta.Name);
        Assert.Equal(20, beta.Size);
        Assert.Equal([betaOld.Key], session.Snapshot.SelectedKeys);
        Assert.False(session.Snapshot.HasMore);
        Assert.Equal(3, session.Snapshot.TotalCount);
    }

    [Fact]
    public async Task LoadedFolderSearchUsesNoProviderCallsAndClearRestoresCachedBrowse()
    {
        var root = TestFileBrowserFactory.Container("root");
        var alpha = TestFileBrowserFactory.File("alpha", root.Key, "Alpha plan.md");
        var beta = TestFileBrowserFactory.File("beta", root.Key, "Beta notes.md");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([beta, alpha]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        session.Select(beta.Key);

        await session.SearchAsync("alpha", FileBrowserSearchScope.LoadedFolder);

        Assert.Equal(1, provider.BrowseCallCount);
        Assert.Equal(0, provider.SearchCallCount);
        Assert.Equal([alpha.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Empty(session.Snapshot.SelectedKeys);
        Assert.Equal("loaded-folder", session.Snapshot.Search!.StrategyId);
        Assert.Equal(2, session.Snapshot.Search.ScannedItems);
        Assert.False(session.Snapshot.Search.IsPartial);

        await session.ClearSearchAsync();

        Assert.Equal(1, provider.BrowseCallCount);
        Assert.Equal([beta.Key, alpha.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Null(session.Snapshot.Search);
    }

    [Fact]
    public async Task NativeSearchLoadMoreDeduplicatesUpdatesAndUsesSearchCursor()
    {
        var root = TestFileBrowserFactory.Container("root");
        var browseOnly = TestFileBrowserFactory.File("browse", root.Key);
        var first = TestFileBrowserFactory.File("first", root.Key, "First result");
        var duplicateOld = TestFileBrowserFactory.File("duplicate", root.Key, "Duplicate old", size: 1);
        var duplicateCurrent = TestFileBrowserFactory.File("duplicate", root.Key, "Duplicate old", size: 1);
        var third = TestFileBrowserFactory.File("third", root.Key, "Third result");
        var descriptor = TestFileBrowserFactory.Descriptor(
            capabilities: FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.NativeSearch,
            searchScopes:
            [
                FileBrowserSearchScope.LoadedFolder,
                FileBrowserSearchScope.LoadedDescendants,
                FileBrowserSearchScope.Provider,
                FileBrowserSearchScope.Progressive
            ]);
        var provider = new FakeFileBrowserProvider(descriptor)
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage(
                [browseOnly],
                nextContinuationToken: "browse-page-2",
                totalCount: 2,
                consistencyToken: "browse-revision")),
            SearchHandler = (request, _) => ValueTask.FromResult(
                request.ContinuationToken is null
                    ? new FileBrowserSearchPage(
                        [first, duplicateOld],
                        "provider-index",
                        nextContinuationToken: "search-page-2",
                        totalCount: 3,
                        consistencyToken: "search-revision")
                    : new FileBrowserSearchPage(
                        [duplicateCurrent, third],
                        "provider-index",
                        totalCount: 3,
                        consistencyToken: "search-revision"))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        await session.SearchAsync("result", FileBrowserSearchScope.Provider);
        session.Select(duplicateOld.Key);

        await session.LoadMoreAsync();

        Assert.Equal(2, provider.SearchCallCount);
        Assert.Null(provider.SearchCalls[0].ContinuationToken);
        Assert.Equal("browse-revision", provider.SearchCalls[0].ConsistencyToken);
        Assert.Equal("search-page-2", provider.SearchCalls[1].ContinuationToken);
        Assert.Equal("search-revision", provider.SearchCalls[1].ConsistencyToken);
        Assert.Equal(["first", "duplicate", "third"], session.Snapshot.Items.Select(item => item.Key.Value));
        Assert.Equal("Duplicate old", Assert.Single(
            session.Snapshot.Items,
            item => item.Key == duplicateOld.Key).Name);
        Assert.Equal([duplicateOld.Key], session.Snapshot.SelectedKeys);
        Assert.Equal("provider-index", session.Snapshot.Search!.StrategyId);
        Assert.False(session.Snapshot.HasMore);
        Assert.Null(session.Snapshot.NextContinuationToken);
        Assert.Equal(3, session.Snapshot.TotalCount);
    }

    [Fact]
    public async Task RecursiveBrowseToggleUsesDistinctCacheAndRejectsUnsupportedSource()
    {
        var root = TestFileBrowserFactory.Container("root");
        var recursiveProvider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(
            capabilities: FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.RecursiveBrowse))
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) => ValueTask.FromResult(new FileBrowserPage([
                TestFileBrowserFactory.File(request.IncludeDescendants ? "recursive" : "shallow", root.Key)
            ]))
        };
        await using var recursiveSession = new FileBrowserSession([recursiveProvider]);
        await recursiveSession.InitializeAsync();
        await recursiveSession.SetIncludeDescendantsAsync(true);
        await recursiveSession.SetIncludeDescendantsAsync(false);

        Assert.Equal(2, recursiveProvider.BrowseCallCount);
        Assert.False(recursiveProvider.BrowseCalls[0].IncludeDescendants);
        Assert.True(recursiveProvider.BrowseCalls[1].IncludeDescendants);
        Assert.False(recursiveSession.Snapshot.IncludeDescendants);
        Assert.Equal("shallow", Assert.Single(recursiveSession.Snapshot.Items).Key.Value);

        var unsupportedProvider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("unsupported"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(TestFileBrowserFactory.Container("root", source: "unsupported")),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([]))
        };
        await using var unsupportedSession = new FileBrowserSession([unsupportedProvider]);
        await unsupportedSession.InitializeAsync();

        await unsupportedSession.SetIncludeDescendantsAsync(true);
        Assert.Equal(FileBrowserErrorCode.Unsupported, unsupportedSession.Snapshot.Error!.Code);
        Assert.False(unsupportedSession.Snapshot.IncludeDescendants);
        Assert.Equal(1, unsupportedProvider.BrowseCallCount);
    }

    [Fact]
    public async Task ProviderErrorIsProjectedAndInitializeCanRecoverWithoutPartialState()
    {
        var root = TestFileBrowserFactory.Container("root");
        var providerError = new FileBrowserError(
            FileBrowserErrorCode.Offline,
            "Project storage is offline.",
            isRetryable: true,
            correlationId: "request-7");
        var shouldFail = true;
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => shouldFail
                ? ValueTask.FromException<FileBrowserPage>(new FileBrowserProviderException(providerError))
                : ValueTask.FromResult(new FileBrowserPage([TestFileBrowserFactory.File("recovered", root.Key)]))
        };
        await using var session = new FileBrowserSession([provider]);

        await session.InitializeAsync();

        Assert.Same(providerError, session.Snapshot.Error);
        Assert.Equal(FileBrowserOperationKind.Idle, session.Snapshot.Operation);
        Assert.Null(session.Snapshot.CurrentSource);
        Assert.Null(session.Snapshot.Location);
        Assert.Empty(session.Snapshot.Items);

        shouldFail = false;
        await session.InitializeAsync();

        Assert.Null(session.Snapshot.Error);
        Assert.Equal("recovered", Assert.Single(session.Snapshot.Items).Key.Value);
        Assert.Equal(2, provider.RootCallCount);
        Assert.Equal(2, provider.BrowseCallCount);
    }

    [Fact]
    public async Task UnexpectedProviderExceptionBecomesRetryableProviderFailure()
    {
        var root = TestFileBrowserFactory.Container("root");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromException<FileBrowserPage>(
                new InvalidOperationException("SDK exploded"))
        };
        await using var session = new FileBrowserSession([provider]);

        await session.InitializeAsync();

        var error = Assert.IsType<FileBrowserError>(session.Snapshot.Error);
        Assert.Equal(FileBrowserErrorCode.ProviderFailure, error.Code);
        Assert.Equal("The source could not complete the file browser request.", error.Message);
        Assert.True(error.IsRetryable);
        Assert.Contains("SDK exploded", error.TechnicalDetail, StringComparison.Ordinal);
        Assert.Equal(FileBrowserOperationKind.Idle, session.Snapshot.Operation);
    }

    [Fact]
    public async Task CallerCancellationIsRethrownAndSnapshotReturnsToIdleWithoutError()
    {
        var root = TestFileBrowserFactory.Container("root");
        using var cancellation = new CancellationTokenSource();
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, token) =>
            {
                cancellation.Cancel();
                return ValueTask.FromCanceled<FileBrowserPage>(token);
            }
        };
        await using var session = new FileBrowserSession([provider]);
        var operations = new List<FileBrowserOperationKind>();
        session.Changed += (_, args) => operations.Add(args.Snapshot.Operation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.InitializeAsync(cancellationToken: cancellation.Token).AsTask());

        Assert.Equal(1, provider.RootCallCount);
        Assert.Equal(1, provider.BrowseCallCount);
        Assert.Equal([FileBrowserOperationKind.Initializing, FileBrowserOperationKind.Idle], operations);
        Assert.Equal(FileBrowserOperationKind.Idle, session.Snapshot.Operation);
        Assert.Null(session.Snapshot.Error);
        Assert.Null(session.Snapshot.CurrentSource);
        Assert.Null(session.Snapshot.CurrentContainer);
        Assert.Empty(session.Snapshot.Items);
    }

    [Fact]
    public async Task SelectionSupportsSingleToggleClearAndRejectsInvisibleOccurrences()
    {
        var root = TestFileBrowserFactory.Container("root");
        var alpha = TestFileBrowserFactory.File("alpha", root.Key);
        var beta = TestFileBrowserFactory.File("beta", root.Key);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([alpha, beta]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        session.Select(alpha.Key);
        Assert.Equal([alpha.Key], session.Snapshot.SelectedKeys);

        session.Select(beta.Key, toggle: true);
        Assert.Equal([alpha.Key, beta.Key], session.Snapshot.SelectedKeys.OrderBy(key => key.Value));

        session.Select(alpha.Key, toggle: true);
        Assert.Equal([beta.Key], session.Snapshot.SelectedKeys);

        var beforeClear = session.Snapshot.Revision;
        session.ClearSelection();
        Assert.Empty(session.Snapshot.SelectedKeys);
        Assert.True(session.Snapshot.Revision > beforeClear);
        var afterClear = session.Snapshot.Revision;
        session.ClearSelection();
        Assert.Equal(afterClear, session.Snapshot.Revision);

        Assert.Throws<ArgumentException>(() => session.Select(TestFileBrowserFactory.Key("missing")));
    }

    [Fact]
    public async Task FailedNavigationPreservesCurrentSelectionAndProjectsCorruptPathError()
    {
        var root = TestFileBrowserFactory.Container("root");
        var folder = TestFileBrowserFactory.Container("folder", root.Key);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            PathHandler = (_, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>(
                [root, TestFileBrowserFactory.File("not-a-container", root.Key)]),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([folder]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        session.Select(folder.Key);

        await session.NavigateAsync(folder.Key);

        Assert.Equal([folder.Key], session.Snapshot.SelectedKeys);
        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, session.Snapshot.Error!.Code);
        Assert.Equal(root.Key, session.Snapshot.CurrentContainer!.Key);
        Assert.Equal(1, provider.BrowseCallCount);
    }

    [Theory]
    [InlineData(SourceFailureStage.Root)]
    [InlineData(SourceFailureStage.Path)]
    [InlineData(SourceFailureStage.Browse)]
    public async Task FailedSourceTransitionRestoresProviderLocationHistorySelectionAndSearch(
        SourceFailureStage failureStage)
    {
        var firstRoot = TestFileBrowserFactory.Container("root", source: "first");
        var firstFolder = TestFileBrowserFactory.Container("folder", firstRoot.Key, source: "first");
        var retained = TestFileBrowserFactory.File("retained", firstFolder.Key, "Retained.md", source: "first");
        var first = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("first"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(firstRoot),
            PathHandler = (_, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([firstRoot, firstFolder]),
            BrowseHandler = (request, _) => ValueTask.FromResult(
                request.ParentKey == firstRoot.Key
                    ? new FileBrowserPage([firstFolder])
                    : new FileBrowserPage([retained]))
        };
        var secondRoot = TestFileBrowserFactory.Container("root", source: "second");
        var secondFolder = TestFileBrowserFactory.Container("folder", secondRoot.Key, source: "second");
        var transitionError = new FileBrowserError(
            FileBrowserErrorCode.Offline,
            $"Second source failed during {failureStage}.",
            isRetryable: true);
        var second = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("second"))
        {
            RootHandler = (_, _) => failureStage == SourceFailureStage.Root
                ? ValueTask.FromException<FileBrowserItem>(new FileBrowserProviderException(transitionError))
                : ValueTask.FromResult(secondRoot),
            PathHandler = (_, _, _) => failureStage == SourceFailureStage.Path
                ? ValueTask.FromException<IReadOnlyList<FileBrowserItem>>(
                    new FileBrowserProviderException(transitionError))
                : ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([secondRoot, secondFolder]),
            BrowseHandler = (_, _) => failureStage == SourceFailureStage.Browse
                ? ValueTask.FromException<FileBrowserPage>(new FileBrowserProviderException(transitionError))
                : ValueTask.FromResult(new FileBrowserPage([]))
        };
        await using var session = new FileBrowserSession([first, second]);
        await session.InitializeAsync(first.Descriptor.Id);
        await session.NavigateAsync(firstFolder.Key);
        await session.SearchAsync("retained", FileBrowserSearchScope.LoadedFolder);
        session.Select(retained.Key);

        if (failureStage == SourceFailureStage.Path)
        {
            await session.InitializeAsync(second.Descriptor.Id, secondFolder.Key);
        }
        else
        {
            await session.ChangeSourceAsync(second.Descriptor.Id);
        }

        var snapshot = session.Snapshot;
        Assert.Equal(first.Descriptor, snapshot.CurrentSource);
        Assert.Equal(firstFolder.Key, snapshot.CurrentContainer!.Key);
        Assert.Equal([retained.Key], snapshot.Items.Select(item => item.Key));
        Assert.Equal([retained.Key], snapshot.SelectedKeys);
        Assert.Equal("retained", snapshot.Search!.Query);
        Assert.Equal(FileBrowserSearchScope.LoadedFolder, snapshot.Search.Scope);
        Assert.True(snapshot.CanGoBack);
        Assert.False(snapshot.CanGoForward);
        Assert.True(snapshot.CanGoUp);
        Assert.Same(transitionError, snapshot.Error);
    }

    [Fact]
    public async Task FailedTargetBrowseRestoresSearchSelectionAndHistory()
    {
        var root = TestFileBrowserFactory.Container("root");
        var folder = TestFileBrowserFactory.Container("folder", root.Key);
        var browseError = new FileBrowserError(
            FileBrowserErrorCode.Offline,
            "Folder is temporarily unavailable.",
            isRetryable: true);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            PathHandler = (_, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root, folder]),
            BrowseHandler = (request, _) => request.ParentKey == root.Key
                ? ValueTask.FromResult(new FileBrowserPage([folder]))
                : ValueTask.FromException<FileBrowserPage>(new FileBrowserProviderException(browseError))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        await session.SearchAsync("folder", FileBrowserSearchScope.LoadedFolder);
        session.Select(folder.Key);

        await session.NavigateAsync(folder.Key);

        var snapshot = session.Snapshot;
        Assert.Equal(root.Key, snapshot.CurrentContainer!.Key);
        Assert.Equal([folder.Key], snapshot.Items.Select(item => item.Key));
        Assert.Equal([folder.Key], snapshot.SelectedKeys);
        Assert.Equal("folder", snapshot.Search!.Query);
        Assert.False(snapshot.CanGoBack);
        Assert.False(snapshot.CanGoForward);
        Assert.False(snapshot.CanGoUp);
        Assert.Same(browseError, snapshot.Error);
    }

    [Fact]
    public async Task CorruptTargetPageCannotChangeProtectedPathOrCachedVisibleState()
    {
        var root = TestFileBrowserFactory.Container("root");
        var folder = TestFileBrowserFactory.Container("folder", root.Key);
        var wrongParent = TestFileBrowserFactory.File("wrong-parent", root.Key);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            PathHandler = (_, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root, folder]),
            BrowseHandler = (request, _) => ValueTask.FromResult(
                request.ParentKey == root.Key
                    ? new FileBrowserPage([folder])
                    : new FileBrowserPage([wrongParent]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        var before = session.Snapshot;

        await session.NavigateAsync(folder.Key);

        var after = session.Snapshot;
        Assert.Equal(root.Key, after.CurrentContainer!.Key);
        Assert.Equal([folder.Key], after.Items.Select(item => item.Key));
        Assert.Equal(before.Diagnostics, after.Diagnostics);
        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, after.Error!.Code);
        Assert.False(after.CanGoBack);
    }

    [Theory]
    [InlineData(MalformedPathKind.RootHasParent)]
    [InlineData(MalformedPathKind.Disconnected)]
    [InlineData(MalformedPathKind.DuplicateCycle)]
    [InlineData(MalformedPathKind.Reordered)]
    public async Task MalformedProviderPathIsRejectedWithoutChangingLocation(
        MalformedPathKind malformedPath)
    {
        var root = TestFileBrowserFactory.Container("root");
        var target = TestFileBrowserFactory.Container("target", root.Key);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([target])),
            PathHandler = (_, _, _) => ValueTask.FromResult(CreateMalformedPath(malformedPath, root, target))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        await session.NavigateAsync(target.Key);

        Assert.Equal(root.Key, session.Snapshot.CurrentContainer!.Key);
        Assert.Equal([target.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, session.Snapshot.Error!.Code);
        Assert.False(session.Snapshot.CanGoBack);
        Assert.Equal(1, provider.BrowseCallCount);
    }

    [Theory]
    [InlineData(FailedHistoryTransition.Back)]
    [InlineData(FailedHistoryTransition.Forward)]
    [InlineData(FailedHistoryTransition.Up)]
    public async Task FailedHistoryBrowseRestoresExactNavigationAndVisibleMode(
        FailedHistoryTransition transition)
    {
        var root = TestFileBrowserFactory.Container("root");
        var folder = TestFileBrowserFactory.Container("folder", root.Key);
        var leaf = TestFileBrowserFactory.File("leaf", folder.Key, "Leaf.md");
        FileBrowserItemKey? failingParent = null;
        var browseError = new FileBrowserError(
            FileBrowserErrorCode.Offline,
            $"{transition} target is unavailable.",
            isRetryable: true);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            PathHandler = (_, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root, folder]),
            BrowseHandler = (request, _) => request.ParentKey == failingParent
                ? ValueTask.FromException<FileBrowserPage>(new FileBrowserProviderException(browseError))
                : ValueTask.FromResult(request.ParentKey == root.Key
                    ? new FileBrowserPage([folder])
                    : new FileBrowserPage([leaf]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        await session.NavigateAsync(folder.Key);

        if (transition == FailedHistoryTransition.Forward)
        {
            await session.GoBackAsync();
            await session.SetSortAsync(new FileBrowserSortDescriptor(FileBrowserSortField.Size));
            await session.SearchAsync("folder", FileBrowserSearchScope.LoadedFolder);
            session.Select(folder.Key);
            failingParent = folder.Key;
        }
        else
        {
            await session.SetSortAsync(new FileBrowserSortDescriptor(FileBrowserSortField.Size));
            await session.SearchAsync("leaf", FileBrowserSearchScope.LoadedFolder);
            session.Select(leaf.Key);
            failingParent = root.Key;
        }

        var before = session.Snapshot;
        switch (transition)
        {
            case FailedHistoryTransition.Back:
                await session.GoBackAsync();
                break;
            case FailedHistoryTransition.Forward:
                await session.GoForwardAsync();
                break;
            case FailedHistoryTransition.Up:
                await session.GoUpAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(transition));
        }

        var after = session.Snapshot;
        Assert.Equal(before.CurrentSource, after.CurrentSource);
        Assert.Equal(before.Location, after.Location);
        Assert.Equal(before.Items.Select(item => item.Key), after.Items.Select(item => item.Key));
        Assert.Equal(before.SelectedKeys, after.SelectedKeys);
        Assert.Equal(before.Search, after.Search);
        Assert.Equal(before.CanGoBack, after.CanGoBack);
        Assert.Equal(before.CanGoForward, after.CanGoForward);
        Assert.Equal(before.CanGoUp, after.CanGoUp);
        Assert.Same(browseError, after.Error);
    }

    [Fact]
    public async Task UnsupportedSortIsNormalizedAndLeavesTheLoadedQueryUntouched()
    {
        var root = TestFileBrowserFactory.Container("root");
        var item = TestFileBrowserFactory.File("item", root.Key);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(
            sortFields: [FileBrowserSortField.Name]))
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([item]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        session.Select(item.Key);

        await session.SetSortAsync(new FileBrowserSortDescriptor(FileBrowserSortField.Owner));

        Assert.Equal(FileBrowserSortField.Name, session.Snapshot.Sort.Field);
        Assert.Equal([item.Key], session.Snapshot.Items.Select(candidate => candidate.Key));
        Assert.Equal([item.Key], session.Snapshot.SelectedKeys);
        Assert.Equal(FileBrowserErrorCode.Unsupported, session.Snapshot.Error!.Code);
        Assert.Equal(1, provider.BrowseCallCount);
    }

    [Fact]
    public async Task RetryRepeatsTheExactFailedSourceChangeAndClearsAfterSuccess()
    {
        var firstRoot = TestFileBrowserFactory.Container("root", source: "first");
        var firstItem = TestFileBrowserFactory.File("first", firstRoot.Key, source: "first");
        var first = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("first"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(firstRoot),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([firstItem]))
        };
        var secondRoot = TestFileBrowserFactory.Container("root", source: "second");
        var secondItem = TestFileBrowserFactory.File("second", secondRoot.Key, source: "second");
        var shouldFail = true;
        var sourceError = new FileBrowserError(
            FileBrowserErrorCode.Offline,
            "Second source is offline.",
            isRetryable: true);
        var second = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("second"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(secondRoot),
            BrowseHandler = (_, _) => shouldFail
                ? ValueTask.FromException<FileBrowserPage>(new FileBrowserProviderException(sourceError))
                : ValueTask.FromResult(new FileBrowserPage([secondItem]))
        };
        await using var session = new FileBrowserSession([first, second]);
        await session.InitializeAsync(first.Descriptor.Id);

        await session.ChangeSourceAsync(second.Descriptor.Id);
        Assert.Equal(first.Descriptor, session.Snapshot.CurrentSource);
        Assert.Same(sourceError, session.Snapshot.Error);

        shouldFail = false;
        await session.RetryAsync();

        Assert.Equal(second.Descriptor, session.Snapshot.CurrentSource);
        Assert.Equal([secondItem.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Null(session.Snapshot.Error);
        Assert.Equal(2, second.RootCallCount);
        Assert.Equal(2, second.BrowseCallCount);
        var revisionAfterRecovery = session.Snapshot.Revision;

        await session.RetryAsync();

        Assert.Equal(revisionAfterRecovery, session.Snapshot.Revision);
        Assert.Equal(2, second.RootCallCount);
        Assert.Equal(2, second.BrowseCallCount);
    }

    [Fact]
    public async Task RetryAfterLoadMoreUsesTheSameContinuationAndPreservesFirstPage()
    {
        var root = TestFileBrowserFactory.Container("root");
        var first = TestFileBrowserFactory.File("first", root.Key);
        var second = TestFileBrowserFactory.File("second", root.Key);
        var continuationAttempts = 0;
        var pageError = new FileBrowserError(
            FileBrowserErrorCode.Unavailable,
            "The next page is temporarily unavailable.",
            isRetryable: true);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) =>
            {
                if (request.ContinuationToken is null)
                {
                    return ValueTask.FromResult(new FileBrowserPage(
                        [first],
                        nextContinuationToken: "cursor-2",
                        consistencyToken: "revision-1"));
                }

                continuationAttempts++;
                return continuationAttempts == 1
                    ? ValueTask.FromException<FileBrowserPage>(new FileBrowserProviderException(pageError))
                    : ValueTask.FromResult(new FileBrowserPage(
                        [second],
                        consistencyToken: "revision-1"));
            }
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        session.Select(first.Key);

        await session.LoadMoreAsync();

        Assert.Same(pageError, session.Snapshot.Error);
        Assert.Equal([first.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Equal([first.Key], session.Snapshot.SelectedKeys);
        Assert.Equal("cursor-2", session.Snapshot.NextContinuationToken);

        await session.RetryAsync();

        Assert.Null(session.Snapshot.Error);
        Assert.Equal([first.Key, second.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Equal([first.Key], session.Snapshot.SelectedKeys);
        Assert.Null(session.Snapshot.NextContinuationToken);
        Assert.Equal(2, continuationAttempts);
        Assert.Equal(
            ["cursor-2", "cursor-2"],
            provider.BrowseCalls.Skip(1).Select(request => request.ContinuationToken));
    }

    [Fact]
    public async Task ActionFacadeCombinesBuiltInsDelegatesCustomActionsAndOpensContent()
    {
        var sourceId = TestFileBrowserFactory.Source("actions");
        var root = TestFileBrowserFactory.Container("root", source: "actions");
        var item = new FileBrowserItem(
            new FileBrowserItemKey(sourceId, "report"),
            root.Key,
            "Report.pdf",
            FileBrowserItemKind.File,
            FileBrowserItemCategory.Document,
            displayPath: "/reports/Report.pdf",
            childState: FileBrowserChildState.Empty,
            contentIdentity: new FileBrowserContentIdentity("cid", "bafy-report"),
            capabilities: FileBrowserItemCapabilities.Select
                | FileBrowserItemCapabilities.Open
                | FileBrowserItemCapabilities.OpenInNewTab
                | FileBrowserItemCapabilities.DownloadFile
                | FileBrowserItemCapabilities.CopyPath
                | FileBrowserItemCapabilities.CopyContentIdentity
                | FileBrowserItemCapabilities.CustomActions,
            openUri: "https://files.example/report",
            downloadUri: "https://files.example/report/download");
        var provider = new OptionalActionContentProvider(root, item);
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        var actions = await session.GetActionsAsync(item.Key);

        Assert.Equal(
            [
                FileBrowserActionIds.Open,
                FileBrowserActionIds.OpenInNewTab,
                FileBrowserActionIds.CopyPath,
                FileBrowserActionIds.CopyContentIdentity,
                FileBrowserActionIds.Download,
                "pin"
            ],
            actions.Select(action => action.Id));
        Assert.Equal("Copy path", Assert.Single(
            actions,
            action => action.Id == FileBrowserActionIds.CopyPath).Label);
        Assert.Equal(
            "/reports/Report.pdf",
            (await session.ExecuteActionAsync(new FileBrowserActionRequest(
                item.Key,
                FileBrowserActionIds.CopyPath))).Value);
        Assert.Equal(
            "bafy-report",
            (await session.ExecuteActionAsync(new FileBrowserActionRequest(
                item.Key,
                FileBrowserActionIds.CopyContentIdentity))).Value);
        Assert.Equal(
            "https://files.example/report",
            (await session.ExecuteActionAsync(new FileBrowserActionRequest(
                item.Key,
                FileBrowserActionIds.Open))).NavigationUri);
        Assert.Equal(
            "https://files.example/report/download",
            (await session.ExecuteActionAsync(new FileBrowserActionRequest(
                item.Key,
                FileBrowserActionIds.Download))).NavigationUri);
        Assert.Equal(
            "provider-pin",
            (await session.ExecuteActionAsync(new FileBrowserActionRequest(item.Key, "pin"))).Value);
        Assert.Equal(1, provider.ExecuteCallCount);

        await using var lease = await session.OpenReadAsync(new FileBrowserReadRequest(item.Key));
        using var reader = new StreamReader(lease.Stream, leaveOpen: true);
        Assert.Equal("report-content", await reader.ReadToEndAsync());
        Assert.Equal("application/pdf", lease.MediaType);
        Assert.Equal(1, provider.OpenReadCallCount);
    }

    [Fact]
    public async Task ContainerOpenNavigatesWhileUnsupportedActionAndContentReturnTypedErrors()
    {
        var root = TestFileBrowserFactory.Container("root");
        var folder = TestFileBrowserFactory.Container("folder", root.Key);
        var file = TestFileBrowserFactory.File("file", root.Key);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            PathHandler = (_, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root, folder]),
            BrowseHandler = (request, _) => ValueTask.FromResult(
                request.ParentKey == root.Key
                    ? new FileBrowserPage([folder, file])
                    : new FileBrowserPage([]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        var unsupported = await session.ExecuteActionAsync(new FileBrowserActionRequest(file.Key, "pin"));
        var open = await session.ExecuteActionAsync(new FileBrowserActionRequest(
            folder.Key,
            FileBrowserActionIds.Open));
        var contentError = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            session.OpenReadAsync(new FileBrowserReadRequest(folder.Key)).AsTask());

        Assert.False(unsupported.Succeeded);
        Assert.Equal(FileBrowserErrorCode.Unsupported, unsupported.Error!.Code);
        Assert.True(open.Succeeded);
        Assert.Equal(folder.Key, session.Snapshot.CurrentContainer!.Key);
        Assert.Equal(FileBrowserErrorCode.Unsupported, contentError.Error.Code);
    }

    [Fact]
    public async Task DisposeDuringActiveAndQueuedIoCancelsAndDrainsWithoutPublishing()
    {
        var root = TestFileBrowserFactory.Container("root");
        var initial = TestFileBrowserFactory.File("initial", root.Key);
        var replacement = TestFileBrowserFactory.File("replacement", root.Key);
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource<FileBrowserPage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken refreshToken = default;
        var browseCall = 0;
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, token) =>
            {
                browseCall++;
                if (browseCall == 1)
                {
                    return ValueTask.FromResult(new FileBrowserPage([initial]));
                }

                refreshToken = token;
                token.Register(() => refreshCanceled.TrySetResult());
                refreshStarted.TrySetResult();
                return new ValueTask<FileBrowserPage>(releaseRefresh.Task);
            }
        };
        var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        var publications = 0;
        session.Changed += (_, _) => Interlocked.Increment(ref publications);

        var activeRefresh = session.RefreshAsync().AsTask();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queuedRefresh = session.RefreshAsync().AsTask();
        var publicationsAtDispose = Volatile.Read(ref publications);

        var disposal = session.DisposeAsync().AsTask();

        await refreshCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(refreshToken.IsCancellationRequested);
        Assert.False(disposal.IsCompleted);
        releaseRefresh.SetResult(new FileBrowserPage([replacement]));
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => activeRefresh);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queuedRefresh);
        Assert.Equal(publicationsAtDispose, Volatile.Read(ref publications));
        Assert.Equal([initial.Key], session.Snapshot.Items.Select(item => item.Key));

        await session.DisposeAsync();
    }

    [Fact]
    public async Task DisposeIsIdempotentAndRejectsFurtherUse()
    {
        var root = TestFileBrowserFactory.Container("root");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([]))
        };
        var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => session.ClearSelection());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.RefreshAsync().AsTask());
    }

    private static IReadOnlyList<FileBrowserItem> CreateMalformedPath(
        MalformedPathKind malformedPath,
        FileBrowserItem root,
        FileBrowserItem target)
        => malformedPath switch
        {
            MalformedPathKind.RootHasParent =>
            [
                TestFileBrowserFactory.Container("root", TestFileBrowserFactory.Key("unexpected-parent")),
                target
            ],
            MalformedPathKind.Disconnected =>
            [
                root,
                TestFileBrowserFactory.Container("target", TestFileBrowserFactory.Key("other-parent"))
            ],
            MalformedPathKind.DuplicateCycle => CreateDuplicateCyclePath(root, target),
            MalformedPathKind.Reordered =>
            [
                root,
                TestFileBrowserFactory.Container("grandchild", target.Key),
                target
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(malformedPath))
        };

    private static IReadOnlyList<FileBrowserItem> CreateDuplicateCyclePath(
        FileBrowserItem root,
        FileBrowserItem target)
    {
        var firstLoop = TestFileBrowserFactory.Container("loop", root.Key);
        var repeatedLoop = TestFileBrowserFactory.Container("loop", firstLoop.Key);
        return
        [
            root,
            firstLoop,
            repeatedLoop,
            TestFileBrowserFactory.Container(target.Key.Value, repeatedLoop.Key)
        ];
    }

    public enum SourceFailureStage
    {
        Root,
        Path,
        Browse
    }

    public enum FailedHistoryTransition
    {
        Back,
        Forward,
        Up
    }

    public enum MalformedPathKind
    {
        RootHasParent,
        Disconnected,
        DuplicateCycle,
        Reordered
    }

    private sealed class OptionalActionContentProvider(
        FileBrowserItem root,
        FileBrowserItem item)
        : IFileBrowserProvider, IFileBrowserActionProvider, IFileBrowserContentProvider
    {
        public FileBrowserSourceDescriptor Descriptor { get; } = TestFileBrowserFactory.Descriptor(
            "actions",
            capabilities: FileBrowserSourceCapabilities.PagedBrowse
                | FileBrowserSourceCapabilities.CustomActions
                | FileBrowserSourceCapabilities.ContentRead);

        public int ExecuteCallCount { get; private set; }

        public int OpenReadCallCount { get; private set; }

        public ValueTask<FileBrowserItem> GetRootAsync(
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(root);

        public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
            FileBrowserItemKey itemKey,
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root]);

        public ValueTask<FileBrowserPage> BrowseAsync(
            FileBrowserBrowseRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileBrowserPage([item]));

        public ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
            FileBrowserItemKey itemKey,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>(
            [
                new(FileBrowserActionIds.CopyPath, "Provider copy", "copy_all"),
                new("pin", "Pin", "push_pin")
            ]);

        public ValueTask<FileBrowserActionResult> ExecuteAsync(
            FileBrowserActionRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecuteCallCount++;
            return ValueTask.FromResult(FileBrowserActionResult.Success(value: "provider-pin"));
        }

        public ValueTask<FileBrowserContentLease> OpenReadAsync(
            FileBrowserReadRequest request,
            CancellationToken cancellationToken = default)
        {
            OpenReadCallCount++;
            var content = "report-content"u8.ToArray();
            return ValueTask.FromResult(new FileBrowserContentLease(
                new MemoryStream(content),
                "application/pdf",
                content.Length));
        }
    }
}

