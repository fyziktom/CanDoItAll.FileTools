using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.FileTools.Providers.FileSystem;

/// <summary>
/// Opens one validated filesystem occurrence for the browser and interaction adapters. Keeping the
/// path resolution and handle checks here prevents the interaction bridge from becoming a second
/// authorization implementation.
/// </summary>
internal sealed class FileSystemContentReader
{
    private readonly FileSystemPathResolver pathResolver;
    private readonly FileSystemItemFactory itemFactory;

    public FileSystemContentReader(
        FileSystemPathResolver pathResolver,
        FileSystemItemFactory itemFactory)
    {
        this.pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        this.itemFactory = itemFactory ?? throw new ArgumentNullException(nameof(itemFactory));
    }

    public FileSystemOpenedContent Open(
        FileBrowserItemKey itemKey,
        long offset,
        long? length,
        CancellationToken cancellationToken)
        => OpenCore(itemKey, offset, length, cancellationToken);

    public FileSystemOpenedContent Open(
        FileReference file,
        long offset,
        long? length,
        CancellationToken cancellationToken)
    {
        // This is deliberately an internal, provider-specific adaptation. A host decides when an
        // occurrence has been authorized and issues FileReference(SourceId, relativeOccurrenceKey);
        // the provider does not expose a generic browser-key authorization shortcut.
        var itemKey = new FileBrowserItemKey(
            new FileBrowserSourceId(file.SourceId),
            file.Value,
            file.Revision);
        return OpenCore(itemKey, offset, length, cancellationToken);
    }

    private FileSystemOpenedContent OpenCore(
        FileBrowserItemKey itemKey,
        long offset,
        long? length,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = pathResolver.ResolvePath(itemKey);
        var entry = path[^1];
        if (entry.IsReparsePoint)
        {
            throw FileSystemProviderErrors.Create(
                FileBrowserErrorCode.Unsupported,
                "Content reads through filesystem reparse points are disabled for this source.");
        }

        if (entry.IsDirectory)
        {
            throw FileSystemProviderErrors.Create(
                FileBrowserErrorCode.InvalidOperation,
                "Only file occurrences support content reads.");
        }

        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                entry.LogicalPath,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                    BufferSize = 4096
                });
            cancellationToken.ThrowIfCancellationRequested();

            // Re-resolve after opening so a stable link swap is rejected instead of being retained
            // as an authorized occurrence. The opened handle is also required to remain a file.
            var current = pathResolver.ResolvePath(itemKey)[^1];
            var openedAttributes = File.GetAttributes(stream.SafeFileHandle);
            if (current.IsReparsePoint || openedAttributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw FileSystemProviderErrors.Create(
                    FileBrowserErrorCode.Unsupported,
                    "Content reads through filesystem reparse points are disabled for this source.");
            }

            if (current.IsDirectory || openedAttributes.HasFlag(FileAttributes.Directory))
            {
                throw FileSystemProviderErrors.Create(
                    FileBrowserErrorCode.InvalidOperation,
                    "Only file occurrences support content reads.");
            }

            var totalLength = stream.Length;
            if (offset > totalLength)
            {
                throw FileSystemProviderErrors.Create(
                    FileBrowserErrorCode.InvalidOperation,
                    "The requested content offset is beyond the end of the file.");
            }

            var available = totalLength - offset;
            var returnedLength = length.HasValue
                ? Math.Min(length.Value, available)
                : available;
            var range = new FileSystemRangeReadStream(stream, offset, returnedLength);
            stream = null;
            return new FileSystemOpenedContent(
                range,
                itemFactory.GetMediaType(entry.Name),
                returnedLength);
        }
        finally
        {
            stream?.Dispose();
        }
    }
}

internal sealed record FileSystemOpenedContent(
    Stream Stream,
    string? MediaType,
    long Length);
