namespace CanDoItAll.FileTools.Providers.FileSystem.Tests;

public sealed class OptionsAndDescriptorTests
{
    [Fact]
    public void OptionsNormalizeAnExistingRootAndConfiguredPolicies()
    {
        using var fileSystem = new TemporaryFileSystem();
        var rootWithSeparator = fileSystem.RootPath + Path.DirectorySeparatorChar;

        var options = new FileSystemFileBrowserOptions(
            FileSystemTestFactory.SourceId,
            rootWithSeparator,
            displayName: "  Workspace files  ",
            includeHidden: true,
            reparsePointPolicy: FileSystemReparsePointPolicy.Exclude,
            recommendedPageSize: 20,
            maximumPageSize: 80);

        Assert.Equal(Path.TrimEndingDirectorySeparator(fileSystem.RootPath), options.RootPath);
        Assert.Equal("Workspace files", options.DisplayName);
        Assert.Equal(FileSystemHiddenItemPolicy.Include, options.HiddenItemPolicy);
        Assert.True(options.IncludeHidden);
        Assert.Equal(FileSystemReparsePointPolicy.Exclude, options.ReparsePointPolicy);
        Assert.Equal(20, options.RecommendedPageSize);
        Assert.Equal(80, options.MaximumPageSize);
    }

    [Fact]
    public void OptionsUseRootDirectoryNameAndInertLinksAsDefaults()
    {
        using var fileSystem = new TemporaryFileSystem();

        var options = new FileSystemFileBrowserOptions(
            FileSystemTestFactory.SourceId,
            fileSystem.RootPath);

        Assert.Equal(new DirectoryInfo(fileSystem.RootPath).Name, options.DisplayName);
        Assert.Equal(FileSystemHiddenItemPolicy.Exclude, options.HiddenItemPolicy);
        Assert.False(options.IncludeHidden);
        Assert.Equal(FileSystemReparsePointPolicy.ExposeAsLink, options.ReparsePointPolicy);
    }

    [Fact]
    public void OptionsRejectRelativeAndMissingRoots()
    {
        Assert.Throws<ArgumentException>(() => new FileSystemFileBrowserOptions(
            FileSystemTestFactory.SourceId,
            "relative-root"));

        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var error = Assert.Throws<DirectoryNotFoundException>(() => new FileSystemFileBrowserOptions(
            FileSystemTestFactory.SourceId,
            missing));
        Assert.DoesNotContain(missing, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1001, 1001)]
    [InlineData(10, 0)]
    [InlineData(10, 1001)]
    [InlineData(20, 10)]
    public void OptionsRejectInvalidPageLimits(int recommended, int maximum)
    {
        using var fileSystem = new TemporaryFileSystem();

        Assert.Throws<ArgumentOutOfRangeException>(() => new FileSystemFileBrowserOptions(
            FileSystemTestFactory.SourceId,
            fileSystem.RootPath,
            recommendedPageSize: recommended,
            maximumPageSize: maximum));
    }

    [Fact]
    public void DescriptorAccuratelyAdvertisesTheLiveShallowFilesystemContract()
    {
        using var fileSystem = new TemporaryFileSystem();
        var provider = FileSystemTestFactory.CreateProvider(
            fileSystem,
            includeHidden: true,
            reparsePointPolicy: FileSystemReparsePointPolicy.Exclude,
            recommendedPageSize: 7,
            maximumPageSize: 19);

        var descriptor = provider.Descriptor;
        Assert.Equal(FileSystemTestFactory.SourceId, descriptor.Id);
        Assert.Equal("Test files", descriptor.DisplayName);
        Assert.Equal("folder", descriptor.Icon);
        Assert.Equal(
            FileBrowserSourceCapabilities.PagedBrowse
            | FileBrowserSourceCapabilities.ContentRead
            | FileBrowserSourceCapabilities.RangeRead,
            descriptor.Capabilities);
        Assert.Equal(7, descriptor.RecommendedPageSize);
        Assert.Equal(19, descriptor.MaximumPageSize);
        Assert.Equal(
            [
                FileBrowserSortField.Name,
                FileBrowserSortField.ModifiedAt,
                FileBrowserSortField.Size,
                FileBrowserSortField.Type,
                FileBrowserSortField.Path
            ],
            descriptor.SupportedSortFields.Order());
        Assert.Equal(
            [
                FileBrowserSearchScope.LoadedFolder,
                FileBrowserSearchScope.LoadedDescendants,
                FileBrowserSearchScope.Progressive
            ],
            descriptor.SupportedSearchScopes.Order());
        Assert.Equal("filesystem", descriptor.Metadata["provider"]);
        Assert.Equal("root-relative", descriptor.Metadata["path-scope"]);
        Assert.Equal("included", descriptor.Metadata["hidden-items"]);
        Assert.Equal("excluded", descriptor.Metadata["reparse-points"]);
        Assert.Equal("always-current", descriptor.Metadata["freshness"]);
        Assert.Equal("none", descriptor.Metadata["cache"]);
        Assert.DoesNotContain(descriptor.Metadata.Values, value => value.Contains(fileSystem.RootPath, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fileSystem.RootPath, descriptor.Description!, StringComparison.OrdinalIgnoreCase);
        Assert.False(descriptor.Supports(FileBrowserSourceCapabilities.NativeSearch));
        Assert.True(descriptor.Supports(FileBrowserSourceCapabilities.ContentRead));
        Assert.True(descriptor.Supports(FileBrowserSourceCapabilities.RangeRead));
    }

    [Fact]
    public void ProviderRequiresOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new FileSystemFileBrowserProvider(null!));
    }

    [Fact]
    public void OptionsRejectAFileRootAndUndefinedReparsePolicy()
    {
        using var fileSystem = new TemporaryFileSystem();
        var file = fileSystem.CreateFile("not-a-root.txt");

        Assert.Throws<ArgumentException>(() => new FileSystemFileBrowserOptions(
            FileSystemTestFactory.SourceId,
            file));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileSystemFileBrowserOptions(
            FileSystemTestFactory.SourceId,
            fileSystem.RootPath,
            reparsePointPolicy: (FileSystemReparsePointPolicy)int.MaxValue));
    }

    [Fact]
    public void ConfiguredDisplayNameCannotProjectTheAbsoluteAuthorizationRoot()
    {
        using var fileSystem = new TemporaryFileSystem();
        var alternateSeparators = fileSystem.RootPath.Replace(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var provider = new FileSystemFileBrowserProvider(new FileSystemFileBrowserOptions(
            FileSystemTestFactory.SourceId,
            fileSystem.RootPath,
            displayName: $"Files at {alternateSeparators}"));

        Assert.DoesNotContain(fileSystem.RootPath, provider.Descriptor.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new DirectoryInfo(fileSystem.RootPath).Name, provider.Descriptor.DisplayName);
        Assert.DoesNotContain(fileSystem.RootPath, provider.Descriptor.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OptionsRejectAReparsePointRootWithoutResolvingIt()
    {
        using var target = new TemporaryFileSystem();
        using var links = new TemporaryFileSystem();
        if (!links.TryCreateDirectorySymbolicLink("root-link", target.RootPath, out var linkPath))
        {
            return;
        }

        var error = Assert.Throws<ArgumentException>(() => new FileSystemFileBrowserOptions(
            FileSystemTestFactory.SourceId,
            linkPath));

        Assert.Contains("cannot be a reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(target.RootPath, error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
