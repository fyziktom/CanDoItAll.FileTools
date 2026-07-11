using System.Text;

namespace CanDoItAll.FileTools.Providers.FileSystem.Tests;

internal sealed class TemporaryFileSystem : IDisposable
{
    public TemporaryFileSystem()
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            "CanDoItAll.FileTools.FileSystem.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string CreateDirectory(string relativePath)
    {
        var path = GetPath(relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    public string CreateFile(string relativePath, string content = "content")
        => CreateFile(relativePath, Encoding.UTF8.GetBytes(content));

    public string CreateFile(string relativePath, byte[] content)
    {
        var path = GetPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    public string GetPath(string relativePath)
        => Path.Combine(
            RootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

    public bool TryCreateDirectorySymbolicLink(
        string relativeLinkPath,
        string targetPath,
        out string linkPath)
    {
        linkPath = GetPath(relativeLinkPath);
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
                                               or IOException
                                               or NotSupportedException
                                               or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public bool TryCreateFileSymbolicLink(
        string relativeLinkPath,
        string targetPath,
        out string linkPath)
    {
        linkPath = GetPath(relativeLinkPath);
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
                                               or IOException
                                               or NotSupportedException
                                               or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        var cleanupEnumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true
        };
        foreach (var path in Directory.EnumerateFileSystemEntries(
                     RootPath,
                     "*",
                     cleanupEnumeration))
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch (Exception exception) when (exception is IOException
                                                   or UnauthorizedAccessException
                                                   or FileNotFoundException
                                                   or DirectoryNotFoundException)
            {
                // Cleanup is best effort for entries removed by a test.
            }
        }

        Directory.Delete(RootPath, recursive: true);
    }
}

internal static class FileSystemTestFactory
{
    public static readonly FileBrowserSourceId SourceId = new("local-files");

    public static FileSystemFileBrowserProvider CreateProvider(
        TemporaryFileSystem fileSystem,
        bool includeHidden = false,
        FileSystemReparsePointPolicy reparsePointPolicy = FileSystemReparsePointPolicy.ExposeAsLink,
        int recommendedPageSize = 2,
        int maximumPageSize = 100)
        => new(new FileSystemFileBrowserOptions(
            SourceId,
            fileSystem.RootPath,
            "Test files",
            includeHidden,
            reparsePointPolicy,
            recommendedPageSize,
            maximumPageSize));

    public static FileBrowserItemKey Key(string value, string? revision = null)
        => new(SourceId, value, revision);

    public static FileReference Reference(string value, string? revision = null)
        => new(SourceId.Value, value, revision);

    public static FileBrowserBrowseRequest Browse(
        string parent = ".",
        int pageSize = 50,
        string? continuationToken = null,
        FileBrowserSortDescriptor? sort = null,
        FileBrowserFilter? filter = null,
        bool includeDescendants = false,
        string? consistencyToken = null,
        FileBrowserMetadataRequest? metadata = null)
        => new(
            Key(parent),
            pageSize,
            continuationToken,
            sort,
            filter,
            includeDescendants,
            consistencyToken,
            metadata);

    public static async Task<FileBrowserProviderException> BrowseErrorAsync(
        FileSystemFileBrowserProvider provider,
        FileBrowserBrowseRequest request)
        => await Assert.ThrowsAsync<FileBrowserProviderException>(
            async () => await provider.BrowseAsync(request));

    public static async Task<FileBrowserProviderException> PathErrorAsync(
        FileSystemFileBrowserProvider provider,
        FileBrowserItemKey key)
        => await Assert.ThrowsAsync<FileBrowserProviderException>(
            async () => await provider.GetPathAsync(key, FileBrowserMetadataRequest.Standard));

    public static async Task<FileBrowserProviderException> ReadErrorAsync(
        FileSystemFileBrowserProvider provider,
        FileBrowserReadRequest request)
        => await Assert.ThrowsAsync<FileBrowserProviderException>(
            async () => await provider.OpenReadAsync(request));

    public static async Task<FileBrowserProviderException> InteractionReadErrorAsync(
        FileSystemFileBrowserProvider provider,
        FileContentReadRequest request)
        => await Assert.ThrowsAsync<FileBrowserProviderException>(
            async () => await provider.OpenReadAsync(request));

    public static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
