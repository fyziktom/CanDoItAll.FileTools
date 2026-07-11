namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Visual/behavioral intent for a provider action.</summary>
public enum FileBrowserActionTone
{
    Neutral,
    Primary,
    Danger
}

/// <summary>Describes a provider or host action without embedding executable UI behavior.</summary>
public sealed record FileBrowserActionDescriptor
{
    public FileBrowserActionDescriptor(
        string id,
        string label,
        string icon,
        FileBrowserActionTone tone = FileBrowserActionTone.Neutral,
        bool isPrimary = false,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        Id = id.Trim();
        Label = label.Trim();
        Icon = icon.Trim();
        Tone = tone;
        IsPrimary = isPrimary;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public string Id { get; }

    public string Label { get; }

    public string Icon { get; }

    public FileBrowserActionTone Tone { get; }

    public bool IsPrimary { get; }

    public string? Description { get; }
}

/// <summary>Requests execution of a provider or host action.</summary>
public sealed record FileBrowserActionRequest
{
    public FileBrowserActionRequest(
        FileBrowserItemKey itemKey,
        string actionId,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(itemKey.Value))
        {
            throw new ArgumentException("A valid item key is required.", nameof(itemKey));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ItemKey = itemKey;
        ActionId = actionId.Trim();
        Parameters = parameters is null
            ? null
            : new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(parameters, StringComparer.Ordinal));
    }

    public FileBrowserItemKey ItemKey { get; }

    public string ActionId { get; }

    public IReadOnlyDictionary<string, string>? Parameters { get; }
}

/// <summary>Outcome of a provider action.</summary>
public sealed record FileBrowserActionResult
{
    public FileBrowserActionResult(
        bool succeeded,
        string? message = null,
        string? navigationUri = null,
        FileBrowserError? error = null,
        string? value = null)
    {
        Succeeded = succeeded;
        Message = message;
        NavigationUri = FileBrowserUriNormalizer.Normalize(navigationUri, nameof(navigationUri));
        Error = error;
        Value = value;
    }

    public bool Succeeded { get; }

    public string? Message { get; }

    public string? NavigationUri { get; }

    public FileBrowserError? Error { get; }

    public string? Value { get; }

    public static FileBrowserActionResult Success(
        string? message = null,
        string? navigationUri = null,
        string? value = null)
        => new(true, message, navigationUri, value: value);

    public static FileBrowserActionResult Failure(FileBrowserError error)
        => new(false, error.Message, null, error);
}

/// <summary>Requests a bounded content read.</summary>
public sealed record FileBrowserReadRequest(
    FileBrowserItemKey ItemKey,
    long Offset = 0,
    long? Length = null)
{
    public FileBrowserItemKey ItemKey { get; } = string.IsNullOrWhiteSpace(ItemKey.Value)
        ? throw new ArgumentException("A valid item key is required.", nameof(ItemKey))
        : ItemKey;

    public long Offset { get; } = Offset < 0
        ? throw new ArgumentOutOfRangeException(nameof(Offset))
        : Offset;

    public long? Length { get; } = Length <= 0
        ? throw new ArgumentOutOfRangeException(nameof(Length))
        : Length;
}

/// <summary>An owned content stream returned by an optional content provider.</summary>
public sealed class FileBrowserContentLease : IAsyncDisposable
{
    private readonly bool ownsStream;
    private int disposed;

    public FileBrowserContentLease(Stream stream, string? mediaType = null, long? length = null, bool ownsStream = true)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
        Length = length;
        this.ownsStream = ownsStream;
    }

    public Stream Stream { get; }

    public string? MediaType { get; }

    /// <summary>
    /// Gets the number of bytes available through this lease when known. For a range request this
    /// is the returned range length, not the total underlying file length.
    /// </summary>
    public long? Length { get; }

    public ValueTask DisposeAsync()
        => ownsStream && Interlocked.Exchange(ref disposed, 1) == 0
            ? Stream.DisposeAsync()
            : ValueTask.CompletedTask;
}
