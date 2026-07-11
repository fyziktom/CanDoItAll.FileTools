using System.Text;

namespace CanDoItAll.FileTools.Providers.FileSystem.Tests;

public sealed class ContentReadTests
{
    [Fact]
    public void ProviderExposesBrowseAndContentContractsButNoActionExecutionContract()
    {
        using var fileSystem = new TemporaryFileSystem();
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        Assert.IsAssignableFrom<IFileBrowserProvider>(provider);
        Assert.IsAssignableFrom<IFileBrowserContentProvider>(provider);
        Assert.False((object)provider is IFileBrowserActionProvider);
        Assert.DoesNotContain(
            typeof(IFileBrowserActionProvider),
            typeof(FileSystemFileBrowserProvider).GetInterfaces());
    }

    [Fact]
    public async Task FullReadReturnsOwnedCurrentContentAndProviderLocalMediaType()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("notes.md", "hello world");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        await using var lease = await provider.OpenReadAsync(
            new FileBrowserReadRequest(FileSystemTestFactory.Key("notes.md")));
        var bytes = await FileSystemTestFactory.ReadAllAsync(lease.Stream);

        Assert.Equal("hello world", Encoding.UTF8.GetString(bytes));
        Assert.Equal(11, lease.Length);
        Assert.Equal("text/markdown", lease.MediaType);
        Assert.Equal(11, lease.Stream.Length);
    }

    [Theory]
    [InlineData(0, null, "0123456789", 10)]
    [InlineData(2, 3L, "234", 3)]
    [InlineData(8, 99L, "89", 2)]
    [InlineData(10, null, "", 0)]
    public async Task RangeReadsClampToEofAndReportReturnedByteLength(
        long offset,
        long? requestedLength,
        string expected,
        long expectedLength)
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("data.txt", "0123456789");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        await using var lease = await provider.OpenReadAsync(
            new FileBrowserReadRequest(
                FileSystemTestFactory.Key("data.txt"),
                offset,
                requestedLength));
        var bytes = await FileSystemTestFactory.ReadAllAsync(lease.Stream);

        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
        Assert.Equal(expectedLength, lease.Length);
        Assert.Equal(expectedLength, lease.Stream.Length);
    }

    [Fact]
    public async Task RangeStreamBoundsReadsAndUsesRangeRelativeSeeking()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("data.txt", "abcdefghij");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        await using var lease = await provider.OpenReadAsync(
            new FileBrowserReadRequest(FileSystemTestFactory.Key("data.txt"), Offset: 2, Length: 4));
        var stream = lease.Stream;
        var oversizedBuffer = new byte[32];

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(4, stream.Length);
        Assert.Equal(0, stream.Position);
        Assert.Equal(4, await stream.ReadAsync(oversizedBuffer));
        Assert.Equal("cdef", Encoding.UTF8.GetString(oversizedBuffer, 0, 4));
        Assert.Equal(4, stream.Position);
        Assert.Equal(0, await stream.ReadAsync(oversizedBuffer));

        Assert.Equal(0, stream.Seek(0, SeekOrigin.Begin));
        Assert.Equal(2, stream.Read(oversizedBuffer, 0, 2));
        Assert.Equal("cd", Encoding.UTF8.GetString(oversizedBuffer, 0, 2));
        Assert.Equal(3, stream.Seek(-1, SeekOrigin.End));
        Assert.Equal('f', (char)stream.ReadByte());
        Assert.Equal(4, stream.Position);
        Assert.Throws<IOException>(() => stream.Seek(1, SeekOrigin.End));
        Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.WriteByte(0));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(1));
    }

    [Fact]
    public async Task OffsetBeyondEofIsRejectedWithoutReturningAStream()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("short.txt", "123");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var error = await FileSystemTestFactory.ReadErrorAsync(
            provider,
            new FileBrowserReadRequest(FileSystemTestFactory.Key("short.txt"), Offset: 4));

        Assert.Equal(FileBrowserErrorCode.InvalidOperation, error.Error.Code);
        Assert.False(error.Error.IsRetryable);
        Assert.Null(error.Error.TechnicalDetail);
    }

    [Fact]
    public async Task ContainersAndFileLinksDoNotProvideContent()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateDirectory("folder");
        var target = fileSystem.CreateFile("target.txt", "target");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        var containerError = await FileSystemTestFactory.ReadErrorAsync(
            provider,
            new FileBrowserReadRequest(FileSystemTestFactory.Key("folder")));
        Assert.Equal(FileBrowserErrorCode.InvalidOperation, containerError.Error.Code);

        if (!fileSystem.TryCreateFileSymbolicLink("alias.txt", target, out _))
        {
            return;
        }

        var linkError = await FileSystemTestFactory.ReadErrorAsync(
            provider,
            new FileBrowserReadRequest(FileSystemTestFactory.Key("alias.txt")));
        Assert.Equal(FileBrowserErrorCode.Unsupported, linkError.Error.Code);
        var alias = Assert.Single(
            (await provider.BrowseAsync(FileSystemTestFactory.Browse())).Items,
            item => item.Name == "alias.txt");
        Assert.Equal(FileBrowserItemKind.Link, alias.Kind);
        Assert.Equal(FileBrowserItemCapabilities.Select, alias.Capabilities);
    }

    [Fact]
    public async Task NewOpenObservesSamePathReplacementWhileExistingLeaseRemainsValid()
    {
        using var fileSystem = new TemporaryFileSystem();
        var path = fileSystem.CreateFile("current.txt", "old-content");
        var archived = fileSystem.GetPath("archived.txt");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        await using var oldLease = await provider.OpenReadAsync(
            new FileBrowserReadRequest(FileSystemTestFactory.Key("current.txt")));
        File.Move(path, archived);
        fileSystem.CreateFile("current.txt", "new-content");
        await using var newLease = await provider.OpenReadAsync(
            new FileBrowserReadRequest(FileSystemTestFactory.Key("current.txt")));

        Assert.Equal(
            "old-content",
            Encoding.UTF8.GetString(await FileSystemTestFactory.ReadAllAsync(oldLease.Stream)));
        Assert.Equal(
            "new-content",
            Encoding.UTF8.GetString(await FileSystemTestFactory.ReadAllAsync(newLease.Stream)));
        Assert.Equal(11, oldLease.Length);
        Assert.Equal(11, newLease.Length);
    }

    [Fact]
    public async Task DeleteAfterOpenDoesNotPoisonLeaseAndNewOpenReturnsSafeNotFound()
    {
        using var fileSystem = new TemporaryFileSystem();
        var path = fileSystem.CreateFile("ephemeral.txt", "still-readable");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);

        await using var lease = await provider.OpenReadAsync(
            new FileBrowserReadRequest(FileSystemTestFactory.Key("ephemeral.txt")));
        File.Delete(path);
        var error = await FileSystemTestFactory.ReadErrorAsync(
            provider,
            new FileBrowserReadRequest(FileSystemTestFactory.Key("ephemeral.txt")));

        Assert.Equal("still-readable", Encoding.UTF8.GetString(await FileSystemTestFactory.ReadAllAsync(lease.Stream)));
        Assert.Equal(FileBrowserErrorCode.NotFound, error.Error.Code);
        Assert.Null(error.Error.TechnicalDetail);
        Assert.DoesNotContain(fileSystem.RootPath, error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisposingLeaseDisposesItsOwnedRangeAndFileStream()
    {
        using var fileSystem = new TemporaryFileSystem();
        fileSystem.CreateFile("owned.txt", "owned");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var lease = await provider.OpenReadAsync(
            new FileBrowserReadRequest(FileSystemTestFactory.Key("owned.txt")));
        var stream = lease.Stream;

        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _ = stream.Length);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await stream.ReadExactlyAsync(new byte[1]));
    }

    [Fact]
    public async Task BrowseRefreshesFileInfoMetadataAfterInPlaceMutation()
    {
        using var fileSystem = new TemporaryFileSystem();
        var path = fileSystem.CreateFile("mutable.txt", "a");
        var provider = FileSystemTestFactory.CreateProvider(fileSystem);
        var request = FileSystemTestFactory.Browse(
            metadata: new FileBrowserMetadataRequest(FileBrowserMetadataFields.All));

        var before = Assert.Single((await provider.BrowseAsync(request)).Items);
        File.WriteAllText(path, "a much longer current value");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        var after = Assert.Single((await provider.BrowseAsync(request)).Items);

        Assert.Equal(1, before.Size);
        Assert.Equal(27, after.Size);
        Assert.NotEqual(before.ModifiedAt, after.ModifiedAt);
    }
}
