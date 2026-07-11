using System.Text;

namespace CanDoItAll.FileTools.Providers.FileSystem.Tests;

public sealed class FileInteractionContentSourceTests
{
    [Fact]
    public void RangeValidationRemainsOwnedByFileInteractionRequestContract()
    {
        var file = FileSystemTestFactory.Reference("notes.txt");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FileContentReadRequest(file, offset: -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FileContentReadRequest(file, length: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FileContentReadRequest(file, length: -1));
    }

    [Fact]
    public void ProviderExposesIndependentReadButNoPersistenceOrActionContracts()
    {
        using var fileSystem = new TemporaryFileSystem();
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        Assert.IsAssignableFrom<IFileContentSource>(provider);
        Assert.False((object)provider is IFileBrowserActionProvider);
        Assert.DoesNotContain(
            typeof(FileSystemFileBrowserProvider).GetMethods(),
            method => method.Name.Contains("Save", StringComparison.Ordinal)
                      || method.Name.Contains("Write", StringComparison.Ordinal)
                      || method.Name.Contains("Delete", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HostIssuedReferenceReadsWithoutBrowserSession()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("notes.md", "hello interaction");
        IFileContentSource source = FileSystemTestFactory.CreateProvider(fileSystem);

        await using var lease = await source.OpenReadAsync(
            new FileContentReadRequest(FileSystemTestFactory.Reference("notes.md")));
        var bytes = await FileSystemTestFactory.ReadAllAsync(lease.Stream);

        Assert.Equal("hello interaction", Encoding.UTF8.GetString(bytes));
        Assert.Equal(17, lease.Length);
        Assert.Equal("text/markdown", lease.MediaType);
        Assert.Null(lease.Revision);
    }

    [Fact]
    public async Task ForeignSourceAndReferenceRevisionAreRejected()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("notes.txt", "content");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var foreign = await FileSystemTestFactory.InteractionReadErrorAsync(
            provider,
            new FileContentReadRequest(new FileReference("foreign-source", "notes.txt")));
        var revised = await FileSystemTestFactory.InteractionReadErrorAsync(
            provider,
            new FileContentReadRequest(FileSystemTestFactory.Reference("notes.txt", "revision-1")));

        Assert.Equal(FileBrowserErrorCode.InvalidLocation, foreign.Error.Code);
        Assert.Equal(FileBrowserErrorCode.InvalidLocation, revised.Error.Code);
        Assert.Null(foreign.Error.TechnicalDetail);
        Assert.Null(revised.Error.TechnicalDetail);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("folder/../../outside.txt")]
    [InlineData("/outside.txt")]
    [InlineData("folder//outside.txt")]
    [InlineData("folder/./outside.txt")]
    public async Task TraversalAndNonCanonicalReferencesCannotEscapeRoot(string occurrenceKey)
    {
        using var fileSystem = new TemporaryFileSystem();
        var outsidePath = Path.Combine(Path.GetDirectoryName(fileSystem.RootPath)!, "outside.txt");
        await File.WriteAllTextAsync(outsidePath, "not authorized");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        try
        {
            var error = await FileSystemTestFactory.InteractionReadErrorAsync(
                provider,
                new FileContentReadRequest(FileSystemTestFactory.Reference(occurrenceKey)));

            Assert.Equal(FileBrowserErrorCode.InvalidLocation, error.Error.Code);
            Assert.DoesNotContain(fileSystem.RootPath, error.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(outsidePath, error.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task DirectoriesAndReparsePointsCannotBeReadThroughInteractionBridge()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory("folder");
        var target = fileSystem.CreateFile("target.txt", "target");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var directoryError = await FileSystemTestFactory.InteractionReadErrorAsync(
            provider,
            new FileContentReadRequest(FileSystemTestFactory.Reference("folder")));
        Assert.Equal(FileBrowserErrorCode.InvalidOperation, directoryError.Error.Code);

        if (!fileSystem.TryCreateFileSymbolicLink("alias.txt", target, out _))
        {
            return;
        }

        var linkError = await FileSystemTestFactory.InteractionReadErrorAsync(
            provider,
            new FileContentReadRequest(FileSystemTestFactory.Reference("alias.txt")));
        Assert.Equal(FileBrowserErrorCode.Unsupported, linkError.Error.Code);
    }

    [Theory]
    [InlineData(0, 5, "01234", 5)]
    [InlineData(2, 5, "23456", 5)]
    [InlineData(8, 5, "89", 2)]
    public async Task InteractionRangeSupportsBoundedMaximumPlusOneProbe(
        long offset,
        long maximumPlusOne,
        string expected,
        long expectedLength)
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("data.txt", "0123456789");
        IFileContentSource source = FileSystemTestFactory.CreateProvider(fileSystem);

        await using var lease = await source.OpenReadAsync(
            new FileContentReadRequest(
                FileSystemTestFactory.Reference("data.txt"),
                offset,
                maximumPlusOne));
        var bytes = await FileSystemTestFactory.ReadAllAsync(lease.Stream);

        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
        Assert.Equal(expectedLength, lease.Length);
        Assert.Equal(expectedLength, lease.Stream.Length);
    }

    [Fact]
    public async Task CancellationIsObservedAndDisposedLeaseOwnsItsStream()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("owned.txt", "owned");
        IFileContentSource source = FileSystemTestFactory.CreateProvider(fileSystem);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await source.OpenReadAsync(
                new FileContentReadRequest(FileSystemTestFactory.Reference("owned.txt")),
                cancellation.Token));

        var lease = await source.OpenReadAsync(
            new FileContentReadRequest(FileSystemTestFactory.Reference("owned.txt")));
        var stream = lease.Stream;
        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _ = stream.Length);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await stream.ReadExactlyAsync(new byte[1]));
    }

    [Fact]
    public async Task EachIndependentOpenObservesCurrentPathReplacement()
    {
        using var fileSystem = new TemporaryFileSystem();
        var currentPath = fileSystem.CreateFile("current.txt", "old-content");
        var archivedPath = fileSystem.GetPath("archived.txt");
        IFileContentSource source = FileSystemTestFactory.CreateProvider(fileSystem);

        await using var oldLease = await source.OpenReadAsync(
            new FileContentReadRequest(FileSystemTestFactory.Reference("current.txt")));
        File.Move(currentPath, archivedPath);
        fileSystem.CreateFile("current.txt", "new-content");
        await using var newLease = await source.OpenReadAsync(
            new FileContentReadRequest(FileSystemTestFactory.Reference("current.txt")));

        Assert.Equal(
            "old-content",
            Encoding.UTF8.GetString(await FileSystemTestFactory.ReadAllAsync(oldLease.Stream)));
        Assert.Equal(
            "new-content",
            Encoding.UTF8.GetString(await FileSystemTestFactory.ReadAllAsync(newLease.Stream)));
        Assert.Null(oldLease.Revision);
        Assert.Null(newLease.Revision);
    }

    [Fact]
    public void ProviderAssemblyHasNoApplicationComponentsOrCacheDependency()
    {
        var references = typeof(FileSystemFileBrowserProvider)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();

        Assert.DoesNotContain(
            references,
            name => name.StartsWith("CanDoItAll", StringComparison.Ordinal)
                    && name != "CanDoItAll.FileTools.Abstractions");
        Assert.DoesNotContain(
            references,
            name => name.Contains("Components", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            name => name.Contains("Caching", StringComparison.OrdinalIgnoreCase));
    }
}
