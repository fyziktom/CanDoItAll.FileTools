namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class FileBrowserDynamicSourcesAndInvalidationTests
{
    [Fact]
    public async Task UpdateSources_PreservesValidLocationAndClearsHistorySearchAndSelection()
    {
        (FakeFileBrowserProvider first, FileBrowserItem root, FileBrowserItem folder) =
            CreateHierarchyProvider("source", "old.txt");
        await using var session = new FileBrowserSession(
            new FileBrowserSourceSet("r1", [first]));
        await session.InitializeAsync();
        await session.NavigateAsync(folder.Key);
        FileBrowserItem selected = Assert.Single(session.Snapshot.Items);
        session.Select(selected.Key);
        await session.SearchAsync("old", FileBrowserSearchScope.LoadedFolder);
        (FakeFileBrowserProvider updated, _, _) = CreateHierarchyProvider("source", "new.txt");

        await session.UpdateSourcesAsync(new FileBrowserSourceSet("r2", [updated]));

        Assert.Equal(folder.Key, session.Snapshot.Location!.Key);
        Assert.Equal("new.txt", Assert.Single(session.Snapshot.Items).Name);
        Assert.Empty(session.Snapshot.SelectedKeys);
        Assert.Null(session.Snapshot.Search);
        Assert.False(session.Snapshot.CanGoBack);
        Assert.Equal(1, updated.PathCallCount);
        Assert.Equal("r2", new FileBrowserSourceSet("r2", [updated]).Revision);
    }

    [Fact]
    public async Task UpdateSources_FallsBackToRootWhenPreservedLocationNoLongerExists()
    {
        (FakeFileBrowserProvider first, _, FileBrowserItem folder) =
            CreateHierarchyProvider("source", "old.txt");
        await using var session = new FileBrowserSession(new FileBrowserSourceSet("r1", [first]));
        await session.InitializeAsync();
        await session.NavigateAsync(folder.Key);
        FileBrowserItem newRoot = TestFileBrowserFactory.Container("root");
        var updated = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(newRoot),
            PathHandler = (_, _, _) => ValueTask.FromException<IReadOnlyList<FileBrowserItem>>(
                new FileBrowserProviderException(new FileBrowserError(
                    FileBrowserErrorCode.NotFound,
                    "Folder removed."))),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([]))
        };

        await session.UpdateSourcesAsync(new FileBrowserSourceSet("r2", [updated]));

        Assert.Equal(newRoot.Key, session.Snapshot.Location!.Key);
        Assert.Equal(1, updated.PathCallCount);
        Assert.Equal(1, updated.RootCallCount);
    }

    [Fact]
    public async Task UpdateSources_FallsBackToFirstSourceOrSupportsEmptySet()
    {
        (FakeFileBrowserProvider first, _, _) = CreateHierarchyProvider("first", "first.txt");
        (FakeFileBrowserProvider second, FileBrowserItem secondRoot, _) =
            CreateHierarchyProvider("second", "second.txt");
        await using var session = new FileBrowserSession(new FileBrowserSourceSet("r1", [first]));
        await session.InitializeAsync();

        await session.UpdateSourcesAsync(new FileBrowserSourceSet("r2", [second]));

        Assert.Equal(second.Descriptor.Id, session.Snapshot.CurrentSource!.Id);
        Assert.Equal(secondRoot.Key, session.Snapshot.Location!.Key);

        await session.UpdateSourcesAsync(new FileBrowserSourceSet("r3", []));

        Assert.Empty(session.Snapshot.Sources);
        Assert.Null(session.Snapshot.CurrentSource);
        Assert.Null(session.Snapshot.Location);
        Assert.Empty(session.Snapshot.Items);
    }

    [Fact]
    public async Task UpdateSources_CancelsOldInflightLoadAndNeverPublishesItsResult()
    {
        FileBrowserItem oldRoot = TestFileBrowserFactory.Container("root", source: "old");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldProvider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("old"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(oldRoot),
            BrowseHandler = async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                return new FileBrowserPage([
                    TestFileBrowserFactory.File("old-leaf", oldRoot.Key, source: "old")
                ]);
            }
        };
        (FakeFileBrowserProvider updated, _, _) = CreateHierarchyProvider("new", "new.txt");
        await using var session = new FileBrowserSession(new FileBrowserSourceSet("r1", [oldProvider]));
        var publishedAfterUpdate = new List<FileBrowserSnapshot>();
        ValueTask initialize = session.InitializeAsync();
        await entered.Task;
        session.Changed += (_, args) => publishedAfterUpdate.Add(args.Snapshot);
        ValueTask update = session.UpdateSourcesAsync(new FileBrowserSourceSet("r2", [updated]));
        release.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await initialize);
        await update;

        Assert.Equal(updated.Descriptor.Id, session.Snapshot.CurrentSource!.Id);
        Assert.DoesNotContain(
            publishedAfterUpdate.SelectMany(snapshot => snapshot.Items),
            item => item.Key.SourceId == oldProvider.Descriptor.Id);
    }

    [Fact]
    public async Task UpdateSources_CallerCancellationRestoresCoherentIdlePreviousState()
    {
        (FakeFileBrowserProvider current, FileBrowserItem currentRoot, _) =
            CreateHierarchyProvider("current", "current.txt");
        FileBrowserItem updatedRoot = TestFileBrowserFactory.Container("root", source: "updated");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var updated = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("updated"))
        {
            RootHandler = async (_, cancellationToken) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return updatedRoot;
            },
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([]))
        };
        await using var session = new FileBrowserSession(new FileBrowserSourceSet("r1", [current]));
        await session.InitializeAsync();
        using var cancellation = new CancellationTokenSource();

        ValueTask update = session.UpdateSourcesAsync(
            new FileBrowserSourceSet("r2", [updated]),
            cancellation.Token);
        await entered.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await update);
        Assert.Equal(FileBrowserOperationKind.Idle, session.Snapshot.Operation);
        Assert.Equal(current.Descriptor.Id, session.Snapshot.CurrentSource!.Id);
        Assert.Equal(currentRoot.Key, session.Snapshot.Location!.Key);
        Assert.Null(session.Snapshot.Error);
    }

    [Fact]
    public async Task UpdateSources_SupersededUpdateCannotPublishAndLatestUpdateEndsIdle()
    {
        (FakeFileBrowserProvider current, _, _) = CreateHierarchyProvider("current", "current.txt");
        FileBrowserItem firstRoot = TestFileBrowserFactory.Container("root", source: "first-update");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstUpdate = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("first-update"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(firstRoot),
            BrowseHandler = async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                return new FileBrowserPage([
                    TestFileBrowserFactory.File("stale", firstRoot.Key, "stale.txt", source: "first-update")
                ]);
            }
        };
        FileBrowserItem latestRoot = TestFileBrowserFactory.Container("root", source: "latest");
        var latest = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("latest"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(latestRoot),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([
                TestFileBrowserFactory.File("leaf", latestRoot.Key, "latest.txt", source: "latest")
            ]))
        };
        await using var session = new FileBrowserSession(new FileBrowserSourceSet("r1", [current]));
        await session.InitializeAsync();
        var published = new List<FileBrowserSnapshot>();
        session.Changed += (_, args) => published.Add(args.Snapshot);

        ValueTask first = session.UpdateSourcesAsync(new FileBrowserSourceSet("r2", [firstUpdate]));
        await entered.Task;
        ValueTask second = session.UpdateSourcesAsync(new FileBrowserSourceSet("r3", [latest]));
        release.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
        await second;
        Assert.Equal(FileBrowserOperationKind.Idle, session.Snapshot.Operation);
        Assert.Equal(latest.Descriptor.Id, session.Snapshot.CurrentSource!.Id);
        Assert.Equal("latest.txt", Assert.Single(session.Snapshot.Items).Name);
        Assert.DoesNotContain(
            published.SelectMany(snapshot => snapshot.Items),
            item => item.Key.SourceId == firstUpdate.Descriptor.Id);
    }

    [Fact]
    public async Task UpdateSources_RetryReplaysExactFailedTransition()
    {
        (FakeFileBrowserProvider current, _, _) = CreateHierarchyProvider("current", "current.txt");
        FileBrowserItem updatedRoot = TestFileBrowserFactory.Container("root", source: "updated");
        var browseAttempts = 0;
        var updated = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("updated"))
        {
            RootHandler = (_, _) => ValueTask.FromResult(updatedRoot),
            BrowseHandler = (_, _) => ++browseAttempts == 1
                ? ValueTask.FromException<FileBrowserPage>(new InvalidOperationException("Transient failure."))
                : ValueTask.FromResult(new FileBrowserPage([
                    TestFileBrowserFactory.File("leaf", updatedRoot.Key, "updated.txt", source: "updated")
                ]))
        };
        await using var session = new FileBrowserSession(new FileBrowserSourceSet("r1", [current]));
        await session.InitializeAsync();

        await session.UpdateSourcesAsync(new FileBrowserSourceSet("r2", [updated]));

        Assert.Equal(current.Descriptor.Id, session.Snapshot.CurrentSource!.Id);
        Assert.True(session.Snapshot.Error!.IsRetryable);
        await session.RetryAsync();
        Assert.Equal(updated.Descriptor.Id, session.Snapshot.CurrentSource!.Id);
        Assert.Equal("updated.txt", Assert.Single(session.Snapshot.Items).Name);
        Assert.Null(session.Snapshot.Error);
        Assert.Equal(2, browseAttempts);
    }

    [Fact]
    public async Task InvalidateSource_CancelsBlockedRefreshBeforeItCanPublishOrRetainStaleData()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) => ValueTask.FromResult(new FileBrowserPage([
                TestFileBrowserFactory.File("initial", request.ParentKey, "initial.txt")
            ]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.BrowseHandler = async (request, _) =>
        {
            entered.TrySetResult();
            await release.Task;
            return new FileBrowserPage([
                TestFileBrowserFactory.File("stale", request.ParentKey, "stale.txt")
            ]);
        };
        var published = new List<FileBrowserSnapshot>();
        session.Changed += (_, args) => published.Add(args.Snapshot);

        ValueTask refresh = session.RefreshAsync();
        await entered.Task;
        published.Clear();
        ValueTask invalidation = session.InvalidateSourceAsync(provider.Descriptor.Id);
        release.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await refresh);
        await invalidation;
        Assert.DoesNotContain(
            published.SelectMany(snapshot => snapshot.Items),
            item => item.Name == "stale.txt");
        Assert.Equal(0, session.Snapshot.Diagnostics.CachedContainerQueryCount);

        provider.BrowseHandler = (request, _) => ValueTask.FromResult(new FileBrowserPage([
            TestFileBrowserFactory.File("fresh", request.ParentKey, "fresh.txt")
        ]));
        await session.RefreshAsync();
        Assert.Equal("fresh.txt", Assert.Single(session.Snapshot.Items).Name);
        Assert.Equal(3, provider.BrowseCallCount);
    }

    [Theory]
    [InlineData(FileBrowserSearchScope.LoadedFolder)]
    [InlineData(FileBrowserSearchScope.LoadedDescendants)]
    [InlineData(FileBrowserSearchScope.Provider)]
    [InlineData(FileBrowserSearchScope.Progressive)]
    public async Task InvalidatedRefresh_ReloadsBrowseBeforeReapplyingEverySearchScope(
        FileBrowserSearchScope scope)
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        string currentName = "before.txt";
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(
            capabilities: FileBrowserSourceCapabilities.PagedBrowse
                | FileBrowserSourceCapabilities.NativeSearch,
            searchScopes: Enum.GetValues<FileBrowserSearchScope>()))
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) => ValueTask.FromResult(new FileBrowserPage([
                TestFileBrowserFactory.File("leaf", request.ParentKey, currentName)
            ])),
            SearchHandler = (request, _) => ValueTask.FromResult(new FileBrowserSearchPage(
                [TestFileBrowserFactory.File("leaf", request.ContainerKey, currentName)],
                "native"))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        await session.SearchAsync(".txt", scope);
        currentName = "after.txt";
        await session.InvalidateSourceAsync(provider.Descriptor.Id);
        int browseCallsBeforeRefresh = provider.BrowseCallCount;

        await session.RefreshAsync();

        Assert.True(provider.BrowseCallCount > browseCallsBeforeRefresh);
        Assert.Equal("after.txt", Assert.Single(session.Snapshot.Items).Name);
        Assert.Equal(scope, session.Snapshot.Search!.Scope);
    }

    [Fact]
    public async Task InvalidateItem_MakesActiveCursorStaleUntilRefresh()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) => ValueTask.FromResult(
                request.ContinuationToken is null
                    ? new FileBrowserPage([], "next", consistencyToken: "v1")
                    : new FileBrowserPage([], consistencyToken: "v1"))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        await session.InvalidateItemAsync(root.Key);
        await session.LoadMoreAsync();

        Assert.Equal(FileBrowserErrorCode.StaleCursor, session.Snapshot.Error!.Code);
        Assert.Equal(1, provider.BrowseCallCount);

        await session.RefreshAsync();
        Assert.Null(session.Snapshot.Error);
        Assert.Equal(2, provider.BrowseCallCount);
    }

    [Fact]
    public async Task InvalidateSource_DropsOnlyTargetedReusablePages()
    {
        (FakeFileBrowserProvider first, _, _) = CreateHierarchyProvider("first", "first.txt");
        (FakeFileBrowserProvider second, _, _) = CreateHierarchyProvider("second", "second.txt");
        await using var session = new FileBrowserSession([first, second]);
        await session.InitializeAsync(first.Descriptor.Id);
        await session.ChangeSourceAsync(second.Descriptor.Id);
        Assert.Equal(1, first.BrowseCallCount);
        Assert.Equal(1, second.BrowseCallCount);

        await session.InvalidateSourceAsync(first.Descriptor.Id);
        await session.ChangeSourceAsync(first.Descriptor.Id);
        await session.ChangeSourceAsync(second.Descriptor.Id);

        Assert.Equal(2, first.BrowseCallCount);
        Assert.Equal(1, second.BrowseCallCount);
    }

    [Fact]
    public async Task DisabledSession_RetainsActiveRenderButRevisitsProviderAndReportsZeroCache()
    {
        (FakeFileBrowserProvider provider, _, FileBrowserItem folder) =
            CreateHierarchyProvider("source", "before.txt");
        string rootName = "before.txt";
        provider.BrowseHandler = (request, _) => ValueTask.FromResult(new FileBrowserPage(
            request.ParentKey.Value == "root"
                ? [folder, TestFileBrowserFactory.File("leaf", request.ParentKey, rootName)]
                : []));
        await using var session = new FileBrowserSession(
            [provider],
            new FileBrowserSessionOptions(retentionMode: FileBrowserStateRetentionMode.Disabled));
        await session.InitializeAsync();
        Assert.Equal("before.txt", session.Snapshot.Items.Single(item => !item.IsContainer).Name);
        await session.NavigateAsync(folder.Key);
        rootName = "after.txt";

        await session.GoBackAsync();

        Assert.Equal("after.txt", session.Snapshot.Items.Single(item => !item.IsContainer).Name);
        Assert.Equal(FileBrowserTreeDiagnostics.Empty, session.Snapshot.Diagnostics);
        Assert.Equal(3, provider.BrowseCallCount);
    }

    [Fact]
    public async Task SortChangedDuringSearch_ReloadsBrowseAndKeepsLoadMoreOnTheNewQuery()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        var requestedSort = new FileBrowserSortDescriptor(
            FileBrowserSortField.Size,
            FileBrowserSortDirection.Descending);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) => request.Sort == requestedSort
                ? ValueTask.FromResult(request.ContinuationToken is null
                    ? new FileBrowserPage([
                        TestFileBrowserFactory.File("sorted", root.Key, "sorted-file.txt", size: 20)
                    ], "sorted-next")
                    : new FileBrowserPage([
                        TestFileBrowserFactory.File("more", root.Key, "more-file.txt", size: 10)
                    ]))
                : ValueTask.FromResult(new FileBrowserPage([
                    TestFileBrowserFactory.File("default", root.Key, "default-file.txt", size: 1)
                ]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        await session.SearchAsync("file", FileBrowserSearchScope.LoadedFolder);

        await session.SetSortAsync(requestedSort);
        await session.ClearSearchAsync();
        await session.LoadMoreAsync();

        Assert.Equal(requestedSort, session.Snapshot.Sort);
        Assert.Equal(["sorted-file.txt", "more-file.txt"], session.Snapshot.Items.Select(item => item.Name));
        Assert.Equal(requestedSort, provider.BrowseCalls[^1].Sort);
        Assert.Equal("sorted-next", provider.BrowseCalls[^1].ContinuationToken);
    }

    [Fact]
    public async Task FilterChangedDuringSearch_ReloadsBrowseAndClearShowsTheFilteredQuery()
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root");
        var markdownOnly = new FileBrowserFilter(extensions: [".md"]);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor())
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) => ValueTask.FromResult(new FileBrowserPage(
                request.Filter.Extensions.Contains(".md")
                    ? [TestFileBrowserFactory.File("markdown", root.Key, "filtered-file.md")]
                    : [TestFileBrowserFactory.File("text", root.Key, "default-file.txt")]))
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();
        await session.SearchAsync("file", FileBrowserSearchScope.LoadedFolder);

        await session.SetFilterAsync(markdownOnly);
        await session.ClearSearchAsync();

        Assert.Equal(markdownOnly, session.Snapshot.Filter);
        Assert.Equal("filtered-file.md", Assert.Single(session.Snapshot.Items).Name);
        Assert.Equal(markdownOnly, provider.BrowseCalls[^1].Filter);
    }

    private static (FakeFileBrowserProvider Provider, FileBrowserItem Root, FileBrowserItem Folder)
        CreateHierarchyProvider(string source, string leafName)
    {
        FileBrowserItem root = TestFileBrowserFactory.Container("root", source: source);
        FileBrowserItem folder = TestFileBrowserFactory.Container("folder", root.Key, source: source);
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(source))
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            PathHandler = (key, _, _) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>(
                key == root.Key ? [root] : [root, folder]),
            BrowseHandler = (request, _) => ValueTask.FromResult(new FileBrowserPage(
                request.ParentKey == root.Key
                    ? [folder]
                    : [TestFileBrowserFactory.File("leaf", folder.Key, leafName, source)]))
        };
        return (provider, root, folder);
    }
}
