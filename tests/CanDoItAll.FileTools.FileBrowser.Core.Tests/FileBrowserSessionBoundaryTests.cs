namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class FileBrowserSessionBoundaryTests
{
    [Fact]
    public async Task CanceledBrowsePageIsRejectedBeforeCacheMutationAndCanBeRequestedAgain()
    {
        var root = TestFileBrowserFactory.Container("root");
        var first = TestFileBrowserFactory.File("first", root.Key);
        var second = TestFileBrowserFactory.File("second", root.Key);
        using var cancellation = new CancellationTokenSource();
        var continuationAttempts = 0;
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(
            recommendedPageSize: 1,
            maximumPageSize: 1))
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) =>
            {
                if (request.ContinuationToken is null)
                {
                    return ValueTask.FromResult(new FileBrowserPage(
                        [first],
                        nextContinuationToken: "cursor-2",
                        totalCount: 2,
                        consistencyToken: "revision-1"));
                }

                continuationAttempts++;
                if (continuationAttempts == 1)
                {
                    cancellation.Cancel();
                }

                return ValueTask.FromResult(new FileBrowserPage(
                    [second],
                    totalCount: 2,
                    consistencyToken: "revision-1"));
            }
        };
        await using var session = new FileBrowserSession(
            [provider],
            new FileBrowserSessionOptions(pageSize: 1));
        await session.InitializeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.LoadMoreAsync(cancellation.Token).AsTask());

        Assert.Equal([first.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Equal("cursor-2", session.Snapshot.NextContinuationToken);
        Assert.Null(session.Snapshot.Error);

        await session.LoadMoreAsync();

        Assert.Equal([first.Key, second.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Null(session.Snapshot.NextContinuationToken);
        Assert.Null(session.Snapshot.Error);
        Assert.Equal(2, continuationAttempts);
        Assert.Equal(
            ["cursor-2", "cursor-2"],
            provider.BrowseCalls.Skip(1).Select(request => request.ContinuationToken));
    }

    [Fact]
    public async Task CanceledExactRetryPreservesTheFailedCommandAndReplaysTheSameCursor()
    {
        var root = TestFileBrowserFactory.Container("root");
        var first = TestFileBrowserFactory.File("first", root.Key);
        var second = TestFileBrowserFactory.File("second", root.Key);
        var retryError = new FileBrowserError(
            FileBrowserErrorCode.ProviderFailure,
            "Transient page failure.",
            isRetryable: true);
        using var cancellation = new CancellationTokenSource();
        var continuationAttempts = 0;
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(
            recommendedPageSize: 1,
            maximumPageSize: 1))
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) =>
            {
                if (request.ContinuationToken is null)
                {
                    return ValueTask.FromResult(new FileBrowserPage(
                        [first],
                        nextContinuationToken: "cursor-2",
                        totalCount: 2,
                        consistencyToken: "revision-1"));
                }

                continuationAttempts++;
                if (continuationAttempts == 1)
                {
                    return ValueTask.FromException<FileBrowserPage>(
                        new FileBrowserProviderException(retryError));
                }

                if (continuationAttempts == 2)
                {
                    cancellation.Cancel();
                }

                return ValueTask.FromResult(new FileBrowserPage(
                    [second],
                    totalCount: 2,
                    consistencyToken: "revision-1"));
            }
        };
        await using var session = new FileBrowserSession(
            [provider],
            new FileBrowserSessionOptions(pageSize: 1));
        await session.InitializeAsync();
        await session.LoadMoreAsync();
        Assert.Same(retryError, session.Snapshot.Error);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RetryAsync(cancellation.Token).AsTask());

        Assert.Equal([first.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Equal("cursor-2", session.Snapshot.NextContinuationToken);
        Assert.Same(retryError, session.Snapshot.Error);

        await session.RetryAsync();

        Assert.Equal([first.Key, second.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Null(session.Snapshot.NextContinuationToken);
        Assert.Null(session.Snapshot.Error);
        Assert.Equal(3, continuationAttempts);
        Assert.All(
            provider.BrowseCalls.Skip(1),
            request => Assert.Equal("cursor-2", request.ContinuationToken));
    }

    [Fact]
    public async Task CanceledProgressiveTraversalCannotAdvanceTheNavigationalCache()
    {
        var root = TestFileBrowserFactory.Container("root");
        var first = TestFileBrowserFactory.File("first", root.Key);
        var second = TestFileBrowserFactory.File("second", root.Key);
        var third = TestFileBrowserFactory.File("third", root.Key);
        using var cancellation = new CancellationTokenSource();
        var provider = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(
            capabilities: FileBrowserSourceCapabilities.PagedBrowse,
            recommendedPageSize: 1,
            maximumPageSize: 1,
            searchScopes: [FileBrowserSearchScope.Progressive]))
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (request, _) => request.ContinuationToken switch
            {
                null => ValueTask.FromResult(new FileBrowserPage(
                    [first],
                    nextContinuationToken: "cursor-2",
                    totalCount: 3,
                    consistencyToken: "revision-1")),
                "cursor-2" => ValueTask.FromResult(new FileBrowserPage(
                    [second],
                    nextContinuationToken: "cursor-3",
                    totalCount: 3,
                    consistencyToken: "revision-1")),
                "cursor-3" => CancelAndReturnLastPage(cancellation, third),
                _ => ValueTask.FromException<FileBrowserPage>(new InvalidOperationException("Unexpected cursor."))
            }
        };
        await using var session = new FileBrowserSession(
            [provider],
            new FileBrowserSessionOptions(pageSize: 1));
        await session.InitializeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.SearchAsync(
                "result",
                FileBrowserSearchScope.Progressive,
                cancellation.Token).AsTask());

        Assert.Null(session.Snapshot.Search);
        Assert.Equal([first.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Equal("cursor-2", session.Snapshot.NextContinuationToken);
        Assert.Null(session.Snapshot.Error);

        await session.LoadMoreAsync();

        Assert.Equal([first.Key, second.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Equal("cursor-3", session.Snapshot.NextContinuationToken);
        Assert.Null(session.Snapshot.Error);
    }

    [Fact]
    public async Task SearchAppendPreservesPartialDiagnosticsWarningsAndIdenticalOverlap()
    {
        var root = TestFileBrowserFactory.Container("root");
        var first = TestFileBrowserFactory.File("first", root.Key);
        var overlap = TestFileBrowserFactory.File("overlap", root.Key, "Overlap", size: 10);
        var last = TestFileBrowserFactory.File("last", root.Key);
        var firstWarning = new FileBrowserPageWarning("partial-index", "Index is partial.");
        var secondWarning = new FileBrowserPageWarning("late-warning", "Later page warning.");
        var provider = CreateNativeProvider(
            root,
            request => request.ContinuationToken is null
                ? new FileBrowserSearchPage(
                    [first, overlap],
                    "native-index",
                    nextContinuationToken: "cursor-2",
                    totalCount: 3,
                    isPartial: true,
                    scannedContainers: 2,
                    scannedItems: 5,
                    consistencyToken: "search-revision",
                    warnings: [firstWarning])
                : new FileBrowserSearchPage(
                    [overlap, last],
                    "native-index",
                    totalCount: 3,
                    scannedContainers: 1,
                    scannedItems: 3,
                    consistencyToken: "search-revision",
                    warnings: [secondWarning]));
        await using var session = new FileBrowserSession(
            [provider],
            new FileBrowserSessionOptions(pageSize: 2));
        await session.InitializeAsync();
        await session.SearchAsync("result", FileBrowserSearchScope.Provider);

        await session.LoadMoreAsync();

        Assert.Equal([first.Key, overlap.Key, last.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.True(session.Snapshot.Search!.IsPartial);
        Assert.Equal(2, session.Snapshot.Search.ScannedContainers);
        Assert.Equal(5, session.Snapshot.Search.ScannedItems);
        Assert.Equal(3, session.Snapshot.TotalCount);
        Assert.Equal([firstWarning, secondWarning], session.Snapshot.Warnings);
        Assert.Null(session.Snapshot.Error);
    }

    [Fact]
    public async Task SearchAppendRejectsConflictingOverlapWithoutReplacingFirstPageState()
    {
        var root = TestFileBrowserFactory.Container("root");
        var stable = TestFileBrowserFactory.File("stable", root.Key);
        var oldOverlap = TestFileBrowserFactory.File("overlap", root.Key, "Old", size: 10);
        var changedOverlap = TestFileBrowserFactory.File("overlap", root.Key, "Changed", size: 20);
        var provider = CreateNativeProvider(
            root,
            request => request.ContinuationToken is null
                ? new FileBrowserSearchPage(
                    [stable, oldOverlap],
                    "native-index",
                    nextContinuationToken: "cursor-2",
                    totalCount: 3,
                    consistencyToken: "search-revision",
                    warnings: [new FileBrowserPageWarning("first", "First page warning.")])
                : new FileBrowserSearchPage(
                    [changedOverlap, TestFileBrowserFactory.File("last", root.Key)],
                    "native-index",
                    totalCount: 3,
                    consistencyToken: "search-revision"));
        await using var session = new FileBrowserSession(
            [provider],
            new FileBrowserSessionOptions(pageSize: 2));
        await session.InitializeAsync();
        await session.SearchAsync("result", FileBrowserSearchScope.Provider);

        await session.LoadMoreAsync();

        Assert.Equal([stable.Key, oldOverlap.Key], session.Snapshot.Items.Select(item => item.Key));
        Assert.Equal("Old", Assert.Single(
            session.Snapshot.Items,
            item => item.Key == oldOverlap.Key).Name);
        Assert.Equal("cursor-2", session.Snapshot.NextContinuationToken);
        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, session.Snapshot.Error!.Code);
        Assert.Equal(["first"], session.Snapshot.Warnings.Select(warning => warning.Code));
    }

    [Fact]
    public async Task ContentReadsRequireSourceAndRangeCapabilitiesBeforeProviderInvocation()
    {
        var missingContent = new ContentProvider(FileBrowserSourceCapabilities.PagedBrowse);
        await using (var session = new FileBrowserSession([missingContent]))
        {
            await session.InitializeAsync();
            FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
                () => session.OpenReadAsync(new FileBrowserReadRequest(missingContent.File.Key)).AsTask());
            Assert.Equal(FileBrowserErrorCode.Unsupported, exception.Error.Code);
            Assert.Equal(0, missingContent.OpenReadCallCount);
        }

        var missingRange = new ContentProvider(
            FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.ContentRead,
            source: "content-only");
        await using (var session = new FileBrowserSession([missingRange]))
        {
            await session.InitializeAsync();
            await using FileBrowserContentLease lease = await session.OpenReadAsync(
                new FileBrowserReadRequest(missingRange.File.Key));
            Assert.Equal(1, missingRange.OpenReadCallCount);

            FileBrowserProviderException offset = await Assert.ThrowsAsync<FileBrowserProviderException>(
                () => session.OpenReadAsync(
                    new FileBrowserReadRequest(missingRange.File.Key, Offset: 1)).AsTask());
            FileBrowserProviderException length = await Assert.ThrowsAsync<FileBrowserProviderException>(
                () => session.OpenReadAsync(
                    new FileBrowserReadRequest(missingRange.File.Key, Length: 10)).AsTask());

            Assert.Equal(FileBrowserErrorCode.Unsupported, offset.Error.Code);
            Assert.Equal(FileBrowserErrorCode.Unsupported, length.Error.Code);
            Assert.Equal(1, missingRange.OpenReadCallCount);
        }
    }

    [Fact]
    public async Task RecursiveCapabilityIsCheckedAfterQueuedSourceChangeAcquiresTheGate()
    {
        var firstRoot = TestFileBrowserFactory.Container("root", source: "recursive");
        var first = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor(
            "recursive",
            capabilities: FileBrowserSourceCapabilities.PagedBrowse
                | FileBrowserSourceCapabilities.RecursiveBrowse))
        {
            RootHandler = (_, _) => ValueTask.FromResult(firstRoot),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([]))
        };
        var secondRoot = TestFileBrowserFactory.Container("root", source: "shallow");
        var rootRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRoot = new TaskCompletionSource<FileBrowserItem>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new FakeFileBrowserProvider(TestFileBrowserFactory.Descriptor("shallow"))
        {
            RootHandler = async (_, cancellationToken) =>
            {
                rootRequested.TrySetResult();
                return await releaseRoot.Task.WaitAsync(cancellationToken);
            },
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage([]))
        };
        await using var session = new FileBrowserSession([first, second]);
        await session.InitializeAsync(first.Descriptor.Id);

        Task sourceChange = session.ChangeSourceAsync(second.Descriptor.Id).AsTask();
        await rootRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task recursiveToggle = session.SetIncludeDescendantsAsync(true).AsTask();
        releaseRoot.SetResult(secondRoot);
        await Task.WhenAll(sourceChange, recursiveToggle).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(second.Descriptor.Id, session.Snapshot.CurrentSource!.Id);
        Assert.False(session.Snapshot.IncludeDescendants);
        Assert.Equal(FileBrowserErrorCode.Unsupported, session.Snapshot.Error!.Code);
        Assert.Equal(1, second.BrowseCallCount);
    }

    [Fact]
    public void ActionRequestDefensivelyCopiesParameters()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "original"
        };
        var request = new FileBrowserActionRequest(
            TestFileBrowserFactory.Key("item"),
            "custom",
            parameters);

        parameters["mode"] = "changed";
        parameters["later"] = "value";

        Assert.Equal("original", request.Parameters!["mode"]);
        Assert.False(request.Parameters.ContainsKey("later"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActionDiscoveryRejectsNullListsAndNullEntries(bool nullList)
    {
        var provider = new MalformedOptionalProvider
        {
            Actions = nullList
                ? null
                : [null!]
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => session.GetActionsAsync(provider.File.Key).AsTask());

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, exception.Error.Code);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CustomActionsRequireBothSourceAndItemCapabilities(
        bool sourceAdvertisesCustomActions,
        bool itemAdvertisesCustomActions)
    {
        var provider = new MalformedOptionalProvider(
            sourceAdvertisesCustomActions,
            itemAdvertisesCustomActions)
        {
            Actions = [new FileBrowserActionDescriptor("pin", "Pin", "push_pin")]
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        IReadOnlyList<FileBrowserActionDescriptor> actions = await session.GetActionsAsync(provider.File.Key);
        FileBrowserActionResult result = await session.ExecuteActionAsync(new FileBrowserActionRequest(
            provider.File.Key,
            "pin"));

        Assert.DoesNotContain(actions, action => action.Id == "pin");
        Assert.Equal(0, provider.GetActionsCallCount);
        Assert.False(result.Succeeded);
        Assert.Equal(FileBrowserErrorCode.Unsupported, result.Error!.Code);
        Assert.Equal(0, provider.ExecuteCallCount);
    }

    [Fact]
    public async Task ReservedCustomActionRequiresTheCorrespondingBuiltInCapability()
    {
        var provider = new MalformedOptionalProvider
        {
            Actions = [new FileBrowserActionDescriptor(FileBrowserActionIds.Refresh, "Refresh", "refresh")]
        };
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => session.GetActionsAsync(provider.File.Key).AsTask());

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, exception.Error.Code);
    }

    [Fact]
    public async Task AdvertisedBuiltInWithoutUriCanDelegateWithoutCustomActionCapabilities()
    {
        var provider = new MalformedOptionalProvider(
            sourceAdvertisesCustomActions: false,
            itemAdvertisesCustomActions: false);
        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        FileBrowserActionResult result = await session.ExecuteActionAsync(new FileBrowserActionRequest(
            provider.File.Key,
            FileBrowserActionIds.Open));

        Assert.True(result.Succeeded);
        Assert.Equal(1, provider.ExecuteCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContentReadRejectsNullAndUnreadableLeases(bool nullLease)
    {
        var provider = new MalformedOptionalProvider();
        if (!nullLease)
        {
            var disposedStream = new MemoryStream([1, 2, 3]);
            disposedStream.Dispose();
            provider.ContentLease = new FileBrowserContentLease(disposedStream);
        }

        await using var session = new FileBrowserSession([provider]);
        await session.InitializeAsync();

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(
            () => session.OpenReadAsync(new FileBrowserReadRequest(provider.File.Key)).AsTask());

        Assert.Equal(FileBrowserErrorCode.CorruptProviderResponse, exception.Error.Code);
    }

    private static FakeFileBrowserProvider CreateNativeProvider(
        FileBrowserItem root,
        Func<FileBrowserSearchRequest, FileBrowserSearchPage> search)
    {
        var descriptor = TestFileBrowserFactory.Descriptor(
            capabilities: FileBrowserSourceCapabilities.PagedBrowse
                | FileBrowserSourceCapabilities.NativeSearch,
            maximumPageSize: 2,
            searchScopes: [FileBrowserSearchScope.Provider]);
        return new FakeFileBrowserProvider(descriptor)
        {
            RootHandler = (_, _) => ValueTask.FromResult(root),
            BrowseHandler = (_, _) => ValueTask.FromResult(new FileBrowserPage(
                [],
                consistencyToken: "browse-revision")),
            SearchHandler = (request, _) => ValueTask.FromResult(search(request))
        };
    }

    private static ValueTask<FileBrowserPage> CancelAndReturnLastPage(
        CancellationTokenSource cancellation,
        FileBrowserItem item)
    {
        cancellation.Cancel();
        return ValueTask.FromResult(new FileBrowserPage(
            [item],
            totalCount: 3,
            consistencyToken: "revision-1"));
    }

    private sealed class ContentProvider : IFileBrowserProvider, IFileBrowserContentProvider
    {
        public ContentProvider(FileBrowserSourceCapabilities capabilities, string source = "content")
        {
            Descriptor = TestFileBrowserFactory.Descriptor(source, capabilities: capabilities);
            Root = TestFileBrowserFactory.Container("root", source: source);
            File = TestFileBrowserFactory.File("file", Root.Key, source: source);
        }

        public FileBrowserSourceDescriptor Descriptor { get; }

        public FileBrowserItem Root { get; }

        public FileBrowserItem File { get; }

        public int OpenReadCallCount { get; private set; }

        public ValueTask<FileBrowserItem> GetRootAsync(
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Root);

        public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
            FileBrowserItemKey itemKey,
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([Root]);

        public ValueTask<FileBrowserPage> BrowseAsync(
            FileBrowserBrowseRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileBrowserPage([File]));

        public ValueTask<FileBrowserContentLease> OpenReadAsync(
            FileBrowserReadRequest request,
            CancellationToken cancellationToken = default)
        {
            OpenReadCallCount++;
            return ValueTask.FromResult(new FileBrowserContentLease(new MemoryStream([1, 2, 3])));
        }
    }

    private sealed class MalformedOptionalProvider
        : IFileBrowserProvider, IFileBrowserActionProvider, IFileBrowserContentProvider
    {
        public MalformedOptionalProvider(
            bool sourceAdvertisesCustomActions = true,
            bool itemAdvertisesCustomActions = true)
        {
            Root = TestFileBrowserFactory.Container("root", source: "malformed");
            File = new FileBrowserItem(
                TestFileBrowserFactory.Key("file", source: "malformed"),
                Root.Key,
                "file",
                FileBrowserItemKind.File,
                FileBrowserItemCategory.Document,
                childState: FileBrowserChildState.Empty,
                capabilities: FileBrowserItemCapabilities.Select
                    | FileBrowserItemCapabilities.Open
                    | FileBrowserItemCapabilities.DownloadFile
                    | (itemAdvertisesCustomActions
                        ? FileBrowserItemCapabilities.CustomActions
                        : FileBrowserItemCapabilities.None));
            Descriptor = TestFileBrowserFactory.Descriptor(
                "malformed",
                capabilities: FileBrowserSourceCapabilities.PagedBrowse
                    | FileBrowserSourceCapabilities.ContentRead
                    | (sourceAdvertisesCustomActions
                        ? FileBrowserSourceCapabilities.CustomActions
                        : FileBrowserSourceCapabilities.None));
        }

        public FileBrowserSourceDescriptor Descriptor { get; }

        public FileBrowserItem Root { get; }

        public FileBrowserItem File { get; }

        public IReadOnlyList<FileBrowserActionDescriptor>? Actions { get; init; } = [];

        public FileBrowserContentLease? ContentLease { get; set; }

        public int GetActionsCallCount { get; private set; }

        public int ExecuteCallCount { get; private set; }

        public ValueTask<FileBrowserItem> GetRootAsync(
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Root);

        public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
            FileBrowserItemKey itemKey,
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([Root]);

        public ValueTask<FileBrowserPage> BrowseAsync(
            FileBrowserBrowseRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileBrowserPage([File]));

        public ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
            FileBrowserItemKey itemKey,
            CancellationToken cancellationToken = default)
        {
            GetActionsCallCount++;
            return ValueTask.FromResult(Actions!);
        }

        public ValueTask<FileBrowserActionResult> ExecuteAsync(
            FileBrowserActionRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecuteCallCount++;
            return ValueTask.FromResult(FileBrowserActionResult.Success());
        }

        public ValueTask<FileBrowserContentLease> OpenReadAsync(
            FileBrowserReadRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ContentLease!);
    }
}

