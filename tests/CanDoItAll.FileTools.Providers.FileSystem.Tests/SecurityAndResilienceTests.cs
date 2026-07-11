namespace CanDoItAll.FileTools.Providers.FileSystem.Tests;

public sealed class SecurityAndResilienceTests
{
    public static TheoryData<string> InvalidOccurrenceKeys => new()
    {
        "../outside",
        "folder/../outside",
        "folder/./child",
        "folder//child",
        "folder/",
        "/rooted"
    };

    [Theory]
    [MemberData(nameof(InvalidOccurrenceKeys))]
    public async Task BrowseRejectsTraversalRootedAndNonCanonicalKeys(string key)
    {
        using var fileSystem = new TemporaryFileSystem();
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var error = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse(key));

        Assert.Equal(FileBrowserErrorCode.InvalidLocation, error.Error.Code);
        Assert.False(error.Error.IsRetryable);
        Assert.Null(error.Error.TechnicalDetail);
    }

    [Fact]
    public async Task WindowsOccurrenceKeysRejectBackslashSeparators()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory("folder/child");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var error = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse("folder\\child"));

        Assert.Equal(FileBrowserErrorCode.InvalidLocation, error.Error.Code);
    }

    [Fact]
    public async Task ProviderRejectsForeignSourceAndRevisionedKeys()
    {
        using var fileSystem = new TemporaryFileSystem();
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var foreign = new FileBrowserBrowseRequest(
            new FileBrowserItemKey(new FileBrowserSourceId("another-source"), "."));
        var revised = new FileBrowserBrowseRequest(FileSystemTestFactory.Key(".", "revision-1"));

        var foreignError = await FileSystemTestFactory.BrowseErrorAsync(provider, foreign);
        var revisedError = await FileSystemTestFactory.BrowseErrorAsync(provider, revised);

        Assert.Equal(FileBrowserErrorCode.InvalidLocation, foreignError.Error.Code);
        Assert.Equal(FileBrowserErrorCode.InvalidLocation, revisedError.Error.Code);
    }

    [Fact]
    public async Task LeafItemsCannotBeBrowsedOrResolvedAsContainerPaths()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("leaf.txt");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var browseError = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse("leaf.txt"));
        var pathError = await FileSystemTestFactory.PathErrorAsync(
            provider,
            FileSystemTestFactory.Key("leaf.txt"));

        Assert.Equal(FileBrowserErrorCode.InvalidOperation, browseError.Error.Code);
        Assert.Equal(FileBrowserErrorCode.InvalidOperation, pathError.Error.Code);
    }

    [Fact]
    public async Task BrowseRejectsRecursiveOversizedAndUnsupportedSortRequests()
    {
        using var fileSystem = new TemporaryFileSystem();
        var provider = FileSystemTestFactory.CreateProvider(
            fileSystem,
            maximumPageSize: 5);

        var recursive = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse(includeDescendants: true));
        var oversized = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse(pageSize: 6));
        var unsupportedSort = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse(
                pageSize: 5,
                sort: new FileBrowserSortDescriptor(FileBrowserSortField.Owner)));

        Assert.Equal(FileBrowserErrorCode.Unsupported, recursive.Error.Code);
        Assert.Equal(FileBrowserErrorCode.InvalidOperation, oversized.Error.Code);
        Assert.Equal(FileBrowserErrorCode.Unsupported, unsupportedSort.Error.Code);
    }

    [Fact]
    public async Task MalformedAndCrossQueryContinuationTokensAreStale()
    {
        using var fileSystem = new TemporaryFileSystem();
        foreach (var name in new[] { "a.txt", "b.txt", "c.txt", "d.txt" })
        {
            fileSystem.CreateFile(name);
        }

        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var request = FileSystemTestFactory.Browse(pageSize: 2);
        var first = await provider.BrowseAsync(request);

        var malformed = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse(pageSize: 2, continuationToken: "not-base64!"));
        var changedPageSize = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse(
                pageSize: 3,
                continuationToken: first.NextContinuationToken,
                consistencyToken: first.ConsistencyToken));
        var changedSort = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse(
                pageSize: 2,
                continuationToken: first.NextContinuationToken,
                sort: new FileBrowserSortDescriptor(
                    FileBrowserSortField.Name,
                    FileBrowserSortDirection.Descending),
                consistencyToken: first.ConsistencyToken));

        Assert.Equal(FileBrowserErrorCode.StaleCursor, malformed.Error.Code);
        Assert.True(malformed.Error.IsRetryable);
        Assert.Null(malformed.Error.TechnicalDetail);
        Assert.Equal(FileBrowserErrorCode.StaleCursor, changedPageSize.Error.Code);
        Assert.Equal(FileBrowserErrorCode.StaleCursor, changedSort.Error.Code);
    }

    [Fact]
    public async Task OversizedTrailingAndTruncatedContinuationTokensAreStale()
    {
        using var fileSystem = new TemporaryFileSystem();
        foreach (var name in new[] { "a.txt", "b.txt", "c.txt" })
        {
            fileSystem.CreateFile(name);
        }

        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var request = FileSystemTestFactory.Browse(pageSize: 1);
        var first = await provider.BrowseAsync(request);
        var valid = first.NextContinuationToken!;
        var invalidTokens = new[]
        {
            new string('A', (16 * 1024) + 1),
            AppendTrailingByte(valid),
            valid[..Math.Max(1, valid.Length / 3)]
        };

        foreach (var token in invalidTokens)
        {
            var error = await FileSystemTestFactory.BrowseErrorAsync(
                provider,
                FileSystemTestFactory.Browse(
                    pageSize: 1,
                    continuationToken: token,
                    consistencyToken: first.ConsistencyToken));
            Assert.Equal(FileBrowserErrorCode.StaleCursor, error.Error.Code);
            Assert.True(error.Error.IsRetryable);
            Assert.Null(error.Error.TechnicalDetail);
        }
    }

    [Fact]
    public async Task ContinuationTokenCannotBeReplayedAgainstAnotherParent()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory("folder");
        fileSystem.CreateFile("folder/a.txt");
        fileSystem.CreateFile("folder/b.txt");
        fileSystem.CreateFile("folder/c.txt");
        fileSystem.CreateFile("root-a.txt");
        fileSystem.CreateFile("root-b.txt");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var first = await provider.BrowseAsync(FileSystemTestFactory.Browse(pageSize: 1));

        var error = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse(
                parent: "folder",
                pageSize: 1,
                continuationToken: first.NextContinuationToken));

        Assert.Equal(FileBrowserErrorCode.StaleCursor, error.Error.Code);
        Assert.True(error.Error.IsRetryable);
    }

    [Fact]
    public async Task DirectoryMutationInvalidatesContinuationAndConsistencyTokens()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("a.txt");
        fileSystem.CreateFile("b.txt");
        fileSystem.CreateFile("c.txt");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var request = FileSystemTestFactory.Browse(pageSize: 1);
        var first = await provider.BrowseAsync(request);

        fileSystem.CreateFile("new.txt");

        var withConsistency = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            request.Next(first.NextContinuationToken!, first.ConsistencyToken));
        var cursorAlone = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            request.Next(first.NextContinuationToken!));

        Assert.Equal(FileBrowserErrorCode.StaleCursor, withConsistency.Error.Code);
        Assert.True(withConsistency.Error.IsRetryable);
        Assert.Equal(FileBrowserErrorCode.StaleCursor, cursorAlone.Error.Code);
        Assert.True(cursorAlone.Error.IsRetryable);
    }

    [Fact]
    public async Task MissingRootAndItemsMapToNotFound()
    {
        using var missingItemFileSystem = new TemporaryFileSystem();
        var missingItemProvider = FileSystemTestFactory.CreateProvider(missingItemFileSystem);
        var itemError = await FileSystemTestFactory.PathErrorAsync(
            missingItemProvider,
            FileSystemTestFactory.Key("missing/folder"));

        using var missingRootFileSystem = new TemporaryFileSystem();
        var missingRootProvider = FileSystemTestFactory.CreateProvider(missingRootFileSystem);
        Directory.Delete(missingRootFileSystem.RootPath, recursive: true);
        var rootError = await Assert.ThrowsAsync<FileBrowserProviderException>(
            async () => await missingRootProvider.GetRootAsync(FileBrowserMetadataRequest.Standard));

        Assert.Equal(FileBrowserErrorCode.NotFound, itemError.Error.Code);
        Assert.Equal(FileBrowserErrorCode.NotFound, rootError.Error.Code);
    }

    [Fact]
    public async Task PreCancelledOperationsPreserveCooperativeCancellation()
    {
        using var fileSystem = new TemporaryFileSystem();
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetRootAsync(FileBrowserMetadataRequest.Standard, cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetPathAsync(
                FileSystemTestFactory.Key("."),
                FileBrowserMetadataRequest.Standard,
                cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.BrowseAsync(
                FileSystemTestFactory.Browse(),
                cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.OpenReadAsync(
                new FileBrowserReadRequest(FileSystemTestFactory.Key("missing.txt")),
                cancellation.Token).AsTask());
    }

    [Fact]
    public async Task ProviderErrorsNeverProjectTheAuthorizationRootOrRawTechnicalDetails()
    {
        using var fileSystem = new TemporaryFileSystem();
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var error = await FileSystemTestFactory.ReadErrorAsync(
            provider,
            new FileBrowserReadRequest(FileSystemTestFactory.Key("folder/secret.txt")));

        Assert.Equal(FileBrowserErrorCode.NotFound, error.Error.Code);
        Assert.Null(error.Error.TechnicalDetail);
        Assert.DoesNotContain(fileSystem.RootPath, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fileSystem.RootPath, error.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fileSystem.RootPath, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(error.InnerException);
    }

    private static string AppendTrailingByte(string token)
    {
        var normalized = token.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
        var payload = Convert.FromBase64String(normalized);
        return Convert.ToBase64String([.. payload, (byte)0x7f])
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
