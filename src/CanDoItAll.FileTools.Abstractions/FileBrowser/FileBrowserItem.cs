using System.Collections.ObjectModel;

namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Describes which metadata is exact, approximate, or expensive to resolve.</summary>
public sealed record FileBrowserMetadataState
{
    public static FileBrowserMetadataState Complete { get; } = new(FileBrowserMetadataFields.All);

    public FileBrowserMetadataState(
        FileBrowserMetadataFields exactFields,
        FileBrowserMetadataFields approximateFields = FileBrowserMetadataFields.None,
        FileBrowserMetadataFields expensiveFields = FileBrowserMetadataFields.None,
        FileBrowserCompleteness completeness = FileBrowserCompleteness.Complete)
    {
        ExactFields = exactFields;
        ApproximateFields = approximateFields;
        ExpensiveFields = expensiveFields;
        Completeness = completeness;
    }

    public FileBrowserMetadataFields ExactFields { get; }

    public FileBrowserMetadataFields ApproximateFields { get; }

    public FileBrowserMetadataFields ExpensiveFields { get; }

    public FileBrowserCompleteness Completeness { get; }
}

/// <summary>A provider-neutral occurrence shown by a file browser.</summary>
public sealed record FileBrowserItem
{
    public FileBrowserItem(
        FileBrowserItemKey key,
        FileBrowserItemKey? parentKey,
        string name,
        FileBrowserItemKind kind,
        FileBrowserItemCategory category,
        string? displayPath = null,
        FileBrowserChildState childState = FileBrowserChildState.Unknown,
        int? childCount = null,
        long? size = null,
        string? mediaType = null,
        string? owner = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? modifiedAt = null,
        FileBrowserContentIdentity? contentIdentity = null,
        FileBrowserMetadataState? metadataState = null,
        FileBrowserItemCapabilities capabilities = FileBrowserItemCapabilities.Select,
        string? openUri = null,
        string? downloadUri = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (string.IsNullOrWhiteSpace(key.Value))
        {
            throw new ArgumentException("A valid item key is required.", nameof(key));
        }

        if (parentKey.HasValue && parentKey.Value.SourceId != key.SourceId)
        {
            throw new ArgumentException("An item and its parent must belong to the same source.", nameof(parentKey));
        }

        if (childCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(childCount));
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        if (kind != FileBrowserItemKind.Container && childState == FileBrowserChildState.HasChildren)
        {
            throw new ArgumentException("Only containers can advertise children.", nameof(childState));
        }

        Key = key;
        ParentKey = parentKey;
        Name = name.Trim();
        Kind = kind;
        Category = category;
        DisplayPath = string.IsNullOrWhiteSpace(displayPath) ? null : displayPath;
        ChildState = childState;
        ChildCount = childCount;
        Size = size;
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType;
        Owner = string.IsNullOrWhiteSpace(owner) ? null : owner;
        CreatedAt = createdAt;
        ModifiedAt = modifiedAt;
        ContentIdentity = contentIdentity;
        MetadataState = metadataState ?? FileBrowserMetadataState.Complete;
        Capabilities = capabilities;
        OpenUri = FileBrowserUriNormalizer.Normalize(openUri, nameof(openUri));
        DownloadUri = FileBrowserUriNormalizer.Normalize(downloadUri, nameof(downloadUri));
        Metadata = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
    }

    public FileBrowserItemKey Key { get; }

    public FileBrowserItemKey? ParentKey { get; }

    public string Name { get; }

    public string? DisplayPath { get; }

    public FileBrowserItemKind Kind { get; }

    public FileBrowserItemCategory Category { get; }

    public FileBrowserChildState ChildState { get; }

    public int? ChildCount { get; }

    public long? Size { get; }

    public string? MediaType { get; }

    public string? Owner { get; }

    public DateTimeOffset? CreatedAt { get; }

    public DateTimeOffset? ModifiedAt { get; }

    public FileBrowserContentIdentity? ContentIdentity { get; }

    public FileBrowserMetadataState MetadataState { get; }

    public FileBrowserItemCapabilities Capabilities { get; }

    public string? OpenUri { get; }

    public string? DownloadUri { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public bool IsContainer => Kind == FileBrowserItemKind.Container;

    public bool Supports(FileBrowserItemCapabilities capability) => Capabilities.HasFlag(capability);

}
