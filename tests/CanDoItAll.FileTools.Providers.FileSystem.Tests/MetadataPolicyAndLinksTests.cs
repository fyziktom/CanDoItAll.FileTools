namespace CanDoItAll.FileTools.Providers.FileSystem.Tests;

public sealed class MetadataPolicyAndLinksTests
{
    [Fact]
    public async Task HiddenPolicyExcludesAndIncludesDotPrefixedEntries()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory(".private-folder");
        fileSystem.CreateFile(".secret.txt");
        fileSystem.CreateFile("visible.txt");

        var excluding = FileSystemTestFactory.CreateProvider(fileSystem, includeHidden: false);
        var including = FileSystemTestFactory.CreateProvider(fileSystem, includeHidden: true);

        var excludedPage = await excluding.BrowseAsync(FileSystemTestFactory.Browse());
        var includedPage = await including.BrowseAsync(FileSystemTestFactory.Browse());

        Assert.Equal(["visible.txt"], excludedPage.Items.Select(item => item.Name));
        Assert.Equal(
            [".private-folder", ".secret.txt", "visible.txt"],
            includedPage.Items.Select(item => item.Name));

        var hiddenPathError = await FileSystemTestFactory.PathErrorAsync(
            excluding,
            FileSystemTestFactory.Key(".private-folder"));
        Assert.Equal(FileBrowserErrorCode.NotFound, hiddenPathError.Error.Code);
    }

    [Fact]
    public async Task WindowsHiddenAttributeRespectsConfiguredPolicy()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fileSystem = new TemporaryFileSystem();
        var hiddenPath = fileSystem.CreateFile("hidden.txt");
        File.SetAttributes(hiddenPath, File.GetAttributes(hiddenPath) | FileAttributes.Hidden);
        fileSystem.CreateFile("visible.txt");

        var excluding = FileSystemTestFactory.CreateProvider(fileSystem, includeHidden: false);
        var including = FileSystemTestFactory.CreateProvider(fileSystem, includeHidden: true);

        var excludedPage = await excluding.BrowseAsync(FileSystemTestFactory.Browse());
        var includedPage = await including.BrowseAsync(FileSystemTestFactory.Browse());

        Assert.Equal(["visible.txt"], excludedPage.Items.Select(item => item.Name));
        Assert.Equal(["hidden.txt", "visible.txt"], includedPage.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task RequestedMetadataAndCapabilitiesReflectAvailableFilesystemData()
    {
        using var fileSystem = new TemporaryFileSystem();
        var filePath = fileSystem.CreateFile("report.pdf", new byte[23]);
        var expectedModified = new DateTime(2024, 5, 4, 12, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(filePath, expectedModified);
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var page = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            metadata: new FileBrowserMetadataRequest(FileBrowserMetadataFields.All)));
        var item = Assert.Single(page.Items);

        Assert.Equal(FileSystemTestFactory.Key("report.pdf"), item.Key);
        Assert.Equal(FileSystemTestFactory.Key("."), item.ParentKey);
        Assert.Equal(FileBrowserItemKind.File, item.Kind);
        Assert.Equal(FileBrowserItemCategory.Document, item.Category);
        Assert.Equal(FileBrowserChildState.Empty, item.ChildState);
        Assert.Equal(23, item.Size);
        Assert.Equal("application/pdf", item.MediaType);
        Assert.Equal("report.pdf", item.DisplayPath);
        Assert.NotNull(item.CreatedAt);
        Assert.NotNull(item.ModifiedAt);
        Assert.Equal(expectedModified, item.ModifiedAt!.Value.UtcDateTime);
        Assert.Null(item.Owner);
        Assert.Null(item.ContentIdentity);
        Assert.Null(item.OpenUri);
        Assert.Null(item.DownloadUri);
        Assert.Equal(FileBrowserCompleteness.Partial, item.MetadataState.Completeness);
        Assert.True(item.MetadataState.ExactFields.HasFlag(FileBrowserMetadataFields.Size));
        Assert.True(item.MetadataState.ExactFields.HasFlag(FileBrowserMetadataFields.Timestamps));
        Assert.True(item.MetadataState.ApproximateFields.HasFlag(FileBrowserMetadataFields.MediaType));
        Assert.True(item.MetadataState.ExactFields.HasFlag(FileBrowserMetadataFields.Custom));
        Assert.Equal("report.pdf", item.Metadata["relative-path"]);
        Assert.Equal("false", item.Metadata["is-reparse-point"]);
        Assert.True(item.Supports(FileBrowserItemCapabilities.Select));
        Assert.True(item.Supports(FileBrowserItemCapabilities.Open));
        Assert.False(item.Supports(FileBrowserItemCapabilities.CopyPath));
        Assert.False(item.Supports(FileBrowserItemCapabilities.Navigate));
        Assert.False(item.Supports(FileBrowserItemCapabilities.DownloadFile));
        Assert.False(item.Supports(FileBrowserItemCapabilities.CustomActions));
    }

    [Fact]
    public async Task MinimalMetadataRequestDoesNotPopulateUnrequestedFields()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("data.json", new byte[12]);
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var metadata = new FileBrowserMetadataRequest(
            FileBrowserMetadataFields.Name | FileBrowserMetadataFields.Kind);

        var page = await provider.BrowseAsync(FileSystemTestFactory.Browse(metadata: metadata));
        var item = Assert.Single(page.Items);

        Assert.Null(item.DisplayPath);
        Assert.Null(item.Size);
        Assert.Null(item.MediaType);
        Assert.Null(item.CreatedAt);
        Assert.Null(item.ModifiedAt);
        Assert.Empty(item.Metadata);
        Assert.Equal(FileBrowserCompleteness.Complete, item.MetadataState.Completeness);
        Assert.True(item.Supports(FileBrowserItemCapabilities.Open));
        Assert.False(item.Supports(FileBrowserItemCapabilities.CopyPath));
    }

    [Theory]
    [InlineData("source.cs", FileBrowserItemCategory.Code, "text/x-csharp")]
    [InlineData("image.webp", FileBrowserItemCategory.Image, "image/webp")]
    [InlineData("archive.zip", FileBrowserItemCategory.Archive, "application/zip")]
    [InlineData("table.csv", FileBrowserItemCategory.Data, "text/csv")]
    [InlineData("movie.mp4", FileBrowserItemCategory.Video, "video/mp4")]
    [InlineData("sound.mp3", FileBrowserItemCategory.Audio, "audio/mpeg")]
    [InlineData("unknown.bin", FileBrowserItemCategory.Other, null)]
    public async Task ExtensionMappingProjectsCategoryAndApproximateMediaType(
        string fileName,
        FileBrowserItemCategory category,
        string? mediaType)
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile(fileName);
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var item = Assert.Single((await provider.BrowseAsync(FileSystemTestFactory.Browse())).Items);

        Assert.Equal(category, item.Category);
        Assert.Equal(mediaType, item.MediaType);
        Assert.Equal(
            mediaType is null,
            !item.MetadataState.ApproximateFields.HasFlag(FileBrowserMetadataFields.MediaType));
    }

    [Fact]
    public async Task SortingCanRequestRequiredMetadataEvenWhenCallerRequestsMinimalFields()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("large.txt", new byte[20]);
        fileSystem.CreateFile("small.txt", new byte[2]);
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var request = FileSystemTestFactory.Browse(
            sort: new FileBrowserSortDescriptor(FileBrowserSortField.Size),
            metadata: new FileBrowserMetadataRequest(
                FileBrowserMetadataFields.Name | FileBrowserMetadataFields.Kind));

        var page = await provider.BrowseAsync(request);

        Assert.Equal(["small.txt", "large.txt"], page.Items.Select(item => item.Name));
        Assert.Equal([2L, 20L], page.Items.Select(item => item.Size));
        Assert.All(page.Items, item => Assert.True(
            item.MetadataState.ExactFields.HasFlag(FileBrowserMetadataFields.Size)));
    }

    [Fact]
    public async Task DefaultDirectorySymlinkIsANonNavigableInertLinkOccurrence()
    {
        using var fileSystem = new TemporaryFileSystem();
        var target = fileSystem.CreateDirectory("target");
        fileSystem.CreateFile("target/inside.txt");
        if (!fileSystem.TryCreateDirectorySymbolicLink("alias", target, out _))
        {
            return;
        }

        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var page = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            metadata: new FileBrowserMetadataRequest(FileBrowserMetadataFields.All)));
        var alias = Assert.Single(page.Items, item => item.Name == "alias");

        Assert.Equal(FileBrowserItemKind.Link, alias.Kind);
        Assert.Equal(FileBrowserItemCategory.Link, alias.Category);
        Assert.Equal(FileBrowserChildState.Empty, alias.ChildState);
        Assert.Equal(FileBrowserItemCapabilities.Select, alias.Capabilities);
        Assert.False(alias.Supports(FileBrowserItemCapabilities.Navigate));
        Assert.False(alias.Supports(FileBrowserItemCapabilities.Open));
        Assert.Null(alias.OpenUri);
        Assert.Null(alias.DownloadUri);
        Assert.Equal("true", alias.Metadata["is-reparse-point"]);

        var browseError = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse("alias"));
        var pathError = await FileSystemTestFactory.PathErrorAsync(
            provider,
            FileSystemTestFactory.Key("alias"));
        Assert.Equal(FileBrowserErrorCode.Unsupported, browseError.Error.Code);
        Assert.Equal(FileBrowserErrorCode.Unsupported, pathError.Error.Code);
    }

    [Fact]
    public async Task ExcludedDirectorySymlinkIsAbsentAndUnavailableByKey()
    {
        using var fileSystem = new TemporaryFileSystem();
        var target = fileSystem.CreateDirectory("target");
        fileSystem.CreateFile("target/inside.txt");
        if (!fileSystem.TryCreateDirectorySymbolicLink("alias", target, out _))
        {
            return;
        }

        var provider = FileSystemTestFactory.CreateProvider(
            fileSystem,
            reparsePointPolicy: FileSystemReparsePointPolicy.Exclude);
        var rootPage = await provider.BrowseAsync(FileSystemTestFactory.Browse());
        var browseError = await FileSystemTestFactory.BrowseErrorAsync(
            provider,
            FileSystemTestFactory.Browse("alias"));
        var pathError = await FileSystemTestFactory.PathErrorAsync(
            provider,
            FileSystemTestFactory.Key("alias"));

        Assert.DoesNotContain(rootPage.Items, item => item.Name == "alias");
        Assert.Equal(FileBrowserErrorCode.NotFound, browseError.Error.Code);
        Assert.Equal(FileBrowserErrorCode.NotFound, pathError.Error.Code);
    }

    [Fact]
    public async Task OutOfRootDirectorySymlinkRemainsInertAndDoesNotDiscloseItsTarget()
    {
        using var fileSystem = new TemporaryFileSystem();
        using var outside = new TemporaryFileSystem();
        outside.CreateFile("outside.txt");
        if (!fileSystem.TryCreateDirectorySymbolicLink("escape", outside.RootPath, out _))
        {
            return;
        }

        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var page = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            metadata: new FileBrowserMetadataRequest(FileBrowserMetadataFields.All)));
        var link = Assert.Single(page.Items);

        Assert.Equal(FileBrowserItemKind.Link, link.Kind);
        Assert.Equal("escape", link.DisplayPath);
        Assert.DoesNotContain(link.Metadata.Values, value => value.Contains(outside.RootPath, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(outside.RootPath, link.ToString(), StringComparison.OrdinalIgnoreCase);
        var error = await FileSystemTestFactory.BrowseErrorAsync(provider, FileSystemTestFactory.Browse("escape"));
        Assert.Equal(FileBrowserErrorCode.Unsupported, error.Error.Code);
        Assert.DoesNotContain(outside.RootPath, error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllProjectedPathsAreRootRelativeAndNoHostEffectContractIsImplemented()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("folder/report.md", "# Report");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var page = await provider.BrowseAsync(FileSystemTestFactory.Browse(
            "folder",
            metadata: new FileBrowserMetadataRequest(FileBrowserMetadataFields.All)));
        var item = Assert.Single(page.Items);

        Assert.Equal("folder/report.md", item.DisplayPath);
        Assert.Equal("folder/report.md", item.Metadata["relative-path"]);
        Assert.DoesNotContain(fileSystem.RootPath, item.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.IsAssignableFrom<IFileBrowserProvider>(provider);
        Assert.IsAssignableFrom<IFileBrowserContentProvider>(provider);
        Assert.False((object)provider is IFileBrowserActionProvider);
        Assert.Null(item.OpenUri);
        Assert.Null(item.DownloadUri);
    }
}
