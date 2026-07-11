namespace CanDoItAll.FileTools.FileInteraction;

public sealed record FileContentReadRequest
{
    public FileContentReadRequest(FileReference file, long offset = 0, long? length = null)
    {
        if (string.IsNullOrWhiteSpace(file.SourceId) || string.IsNullOrWhiteSpace(file.Value))
        {
            throw new ArgumentException("A valid file reference is required.", nameof(file));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        File = file;
        Offset = offset;
        Length = length;
    }

    public FileReference File { get; }

    public long Offset { get; }

    public long? Length { get; }
}

/// <summary>Opens content independently of a FileBrowser session lifetime.</summary>
public interface IFileContentSource
{
    ValueTask<FileContentLease> OpenReadAsync(
        FileContentReadRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class FileContentLease : IAsyncDisposable
{
    private readonly bool ownsStream;
    private int disposed;

    public FileContentLease(
        Stream stream,
        string? mediaType = null,
        long? length = null,
        FileContentRevision? revision = null,
        bool ownsStream = true)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        MediaType = FileInteractionMediaType.NormalizeOptional(mediaType);
        Length = length;
        Revision = revision;
        this.ownsStream = ownsStream;
    }

    public Stream Stream { get; }

    public string? MediaType { get; }

    public long? Length { get; }

    public FileContentRevision? Revision { get; }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0 && ownsStream)
        {
            await Stream.DisposeAsync();
        }
    }
}

/// <summary>Produces a fresh owned stream for each host persistence attempt.</summary>
public interface IFileSaveContent
{
    long? Length { get; }

    ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}

public sealed record FileSaveRequest
{
    public FileSaveRequest(
        FileReference file,
        long editRevision,
        IFileSaveContent content,
        FileContentRevision? expectedRevision = null,
        string? mediaType = null,
        string? encodingName = null,
        bool isAutomatic = false)
    {
        if (string.IsNullOrWhiteSpace(file.SourceId) || string.IsNullOrWhiteSpace(file.Value))
        {
            throw new ArgumentException("A valid file reference is required.", nameof(file));
        }

        if (editRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(editRevision));
        }

        File = file;
        EditRevision = editRevision;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ExpectedRevision = expectedRevision;
        MediaType = FileInteractionMediaType.NormalizeOptional(mediaType);
        EncodingName = string.IsNullOrWhiteSpace(encodingName) ? null : encodingName.Trim();
        IsAutomatic = isAutomatic;
    }

    public FileReference File { get; }

    public long EditRevision { get; }

    public IFileSaveContent Content { get; }

    public FileContentRevision? ExpectedRevision { get; }

    public string? MediaType { get; }

    public string? EncodingName { get; }

    public bool IsAutomatic { get; }
}

public sealed class FileSaveConflictException : Exception
{
    public FileSaveConflictException(
        FileReference file,
        FileContentRevision? expectedRevision,
        FileContentRevision? actualRevision,
        string? message = null,
        Exception? innerException = null)
        : base(message ?? "The file changed after editing started.", innerException)
    {
        File = file;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public FileReference File { get; }

    public FileContentRevision? ExpectedRevision { get; }

    public FileContentRevision? ActualRevision { get; }
}
