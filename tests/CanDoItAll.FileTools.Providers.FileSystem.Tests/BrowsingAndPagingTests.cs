namespace CanDoItAll.FileTools.Providers.FileSystem.Tests;

public sealed class BrowsingAndPagingTests
{
    [Fact]
    public async Task RootAndNestedPathPreserveOccurrenceHierarchy()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory("projects/app/src");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var root = await provider.GetRootAsync(FileBrowserMetadataRequest.Standard);
        var path = await provider.GetPathAsync(
            FileSystemTestFactory.Key("projects/app/src"),
            FileBrowserMetadataRequest.Standard);

        Assert.Equal(FileSystemTestFactory.Key("."), root.Key);
        Assert.Null(root.ParentKey);
        Assert.Equal("Test files", root.Name);
        Assert.Equal(FileBrowserItemKind.Container, root.Kind);
        Assert.Equal(FileBrowserItemCategory.Folder, root.Category);
        Assert.Equal(FileBrowserChildState.Unknown, root.ChildState);
        Assert.Equal(".", root.DisplayPath);

        Assert.Equal([".", "projects", "projects/app", "projects/app/src"], path.Select(item => item.Key.Value));
        Assert.Equal(["Test files", "projects", "app", "src"], path.Select(item => item.Name));
        Assert.Equal([".", "projects", "projects/app", "projects/app/src"], path.Select(item => item.DisplayPath));
        Assert.Null(path[0].ParentKey);
        Assert.Equal(path[0].Key, path[1].ParentKey);
        Assert.Equal(path[1].Key, path[2].ParentKey);
        Assert.Equal(path[2].Key, path[3].ParentKey);
        Assert.All(path, item => Assert.Equal(FileBrowserItemKind.Container, item.Kind));
    }

    [Fact]
    public async Task BrowseReturnsOnlyDirectChildren()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory("folder/nested");
        fileSystem.CreateFile("folder/direct.txt");
        fileSystem.CreateFile("folder/nested/deep.txt");
        fileSystem.CreateFile("root.txt");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var rootPage = await provider.BrowseAsync(FileSystemTestFactory.Browse());
        var folderPage = await provider.BrowseAsync(FileSystemTestFactory.Browse("folder"));

        Assert.Equal(["folder", "root.txt"], rootPage.Items.Select(item => item.Name));
        Assert.Equal(["nested", "direct.txt"], folderPage.Items.Select(item => item.Name));
        Assert.DoesNotContain(rootPage.Items, item => item.Key.Value.Contains("deep.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(folderPage.Items, item => item.Key.Value.Contains("deep.txt", StringComparison.Ordinal));
        Assert.Equal(2, rootPage.TotalCount);
        Assert.Equal(FileBrowserCompleteness.Complete, rootPage.Completeness);
    }

    [Fact]
    public async Task PagingReturnsEveryItemOnceWithStableConsistency()
    {
        using var fileSystem = new TemporaryFileSystem();
        foreach (var name in new[] { "echo.txt", "alpha.txt", "delta.txt", "bravo.txt", "charlie.txt" })
        {
            fileSystem.CreateFile(name, name);
        }

        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var request = FileSystemTestFactory.Browse(pageSize: 2);

        var first = await provider.BrowseAsync(request);
        var second = await provider.BrowseAsync(request.Next(first.NextContinuationToken!, first.ConsistencyToken));
        var third = await provider.BrowseAsync(request.Next(second.NextContinuationToken!, second.ConsistencyToken));

        Assert.Equal(["alpha.txt", "bravo.txt"], first.Items.Select(item => item.Name));
        Assert.Equal(["charlie.txt", "delta.txt"], second.Items.Select(item => item.Name));
        Assert.Equal(["echo.txt"], third.Items.Select(item => item.Name));
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(5, second.TotalCount);
        Assert.Equal(5, third.TotalCount);
        Assert.NotNull(first.NextContinuationToken);
        Assert.NotNull(second.NextContinuationToken);
        Assert.Null(third.NextContinuationToken);
        Assert.Equal(first.ConsistencyToken, second.ConsistencyToken);
        Assert.Equal(first.ConsistencyToken, third.ConsistencyToken);
    }

    [Fact]
    public async Task NameSortHonorsDirectionAndFoldersFirstPolicy()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory("alpha-folder");
        fileSystem.CreateFile("zulu.txt");
        fileSystem.CreateFile("bravo.txt");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var foldersFirst = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.Name,
                FileBrowserSortDirection.Descending,
                FoldersFirst: true)));
        var globalNameOrder = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.Name,
                FileBrowserSortDirection.Descending,
                FoldersFirst: false)));

        Assert.Equal(["alpha-folder", "zulu.txt", "bravo.txt"], foldersFirst.Items.Select(item => item.Name));
        Assert.Equal(["zulu.txt", "bravo.txt", "alpha-folder"], globalNameOrder.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task SizeModifiedTypeAndPathSortsAreDeterministic()
    {
        using var fileSystem = new TemporaryFileSystem();
        var small = fileSystem.CreateFile("z-small.txt", new byte[1]);
        var large = fileSystem.CreateFile("a-large.json", new byte[32]);
        var image = fileSystem.CreateFile("m-image.png", new byte[8]);
        File.SetLastWriteTimeUtc(small, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(image, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(large, new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var bySize = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            sort: new FileBrowserSortDescriptor(FileBrowserSortField.Size)));
        var byModified = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.ModifiedAt,
                FileBrowserSortDirection.Descending)));
        var byType = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            sort: new FileBrowserSortDescriptor(FileBrowserSortField.Type)));
        var byPath = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            sort: new FileBrowserSortDescriptor(FileBrowserSortField.Path)));

        Assert.Equal(["z-small.txt", "m-image.png", "a-large.json"], bySize.Items.Select(item => item.Name));
        Assert.Equal(["a-large.json", "m-image.png", "z-small.txt"], byModified.Items.Select(item => item.Name));
        Assert.Equal(["a-large.json", "m-image.png", "z-small.txt"], byType.Items.Select(item => item.Name));
        Assert.Equal(["a-large.json", "m-image.png", "z-small.txt"], byPath.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task FilteringSupportsKindsCategoriesExtensionsAndMediaTypes()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory("folder");
        fileSystem.CreateFile("photo.png");
        fileSystem.CreateFile("code.cs");
        fileSystem.CreateFile("notes.txt");
        fileSystem.CreateFile("unknown.bin");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var containers = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            filter: new FileBrowserFilter(kinds: [FileBrowserItemKind.Container])));
        var code = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            filter: new FileBrowserFilter(categories: [FileBrowserItemCategory.Code])));
        var selectedExtensions = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            filter: new FileBrowserFilter(extensions: ["TXT", ".cs"])));
        var images = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            filter: new FileBrowserFilter(mediaTypePrefix: "image/"),
            metadata: new FileBrowserMetadataRequest(
                FileBrowserMetadataFields.Name | FileBrowserMetadataFields.Kind)));

        Assert.Equal(["folder"], containers.Items.Select(item => item.Name));
        Assert.Equal(["code.cs"], code.Items.Select(item => item.Name));
        Assert.Equal(["folder", "code.cs", "notes.txt"], selectedExtensions.Items.Select(item => item.Name));
        Assert.Equal(["photo.png"], images.Items.Select(item => item.Name));
        Assert.Equal("image/png", Assert.Single(images.Items).MediaType);
    }

    [Fact]
    public async Task EmptyDirectoryReturnsACompleteTerminalPage()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory("empty");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var page = await provider.BrowseAsync(FileSystemTestFactory.Browse("empty", pageSize: 1));

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.False(page.HasMore);
        Assert.NotNull(page.ConsistencyToken);
        Assert.Equal(FileBrowserCompleteness.Complete, page.Completeness);
        Assert.Empty(page.Warnings);
    }

    [Fact]
    public async Task RepeatedFirstPagesObserveCurrentMutationsWithoutACache()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("before.txt", "before");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var request = FileSystemTestFactory.Browse(pageSize: 10);

        var before = await provider.BrowseAsync(request);
        File.Delete(fileSystem.GetPath("before.txt"));
        fileSystem.CreateFile("after.txt", "after");
        var after = await provider.BrowseAsync(request);

        Assert.Equal(["before.txt"], before.Items.Select(item => item.Name));
        Assert.Equal(["after.txt"], after.Items.Select(item => item.Name));
        Assert.NotEqual(before.ConsistencyToken, after.ConsistencyToken);
    }
}
