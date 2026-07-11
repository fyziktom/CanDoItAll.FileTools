namespace CanDoItAll.FileTools.FileBrowser.Tests;

internal static class TestFileBrowserFactory
{
    public static FileBrowserSourceId Source(string value = "source") => new(value);

    public static FileBrowserItemKey Key(
        string value,
        string source = "source",
        string? revision = null)
        => new(Source(source), value, revision);

    public static FileBrowserItem Container(
        string value,
        FileBrowserItemKey? parentKey = null,
        string? name = null,
        string source = "source",
        string? revision = null,
        FileBrowserChildState childState = FileBrowserChildState.HasChildren,
        int? childCount = null,
        string? displayPath = null,
        DateTimeOffset? modifiedAt = null,
        string? owner = null)
    {
        var key = Key(value, source, revision);
        return new FileBrowserItem(
            key,
            parentKey,
            name ?? value,
            FileBrowserItemKind.Container,
            FileBrowserItemCategory.Folder,
            displayPath,
            childState,
            childCount,
            owner: owner,
            modifiedAt: modifiedAt,
            capabilities: FileBrowserItemCapabilities.Select
                | FileBrowserItemCapabilities.Navigate
                | FileBrowserItemCapabilities.CopyPath);
    }

    public static FileBrowserItem File(
        string value,
        FileBrowserItemKey? parentKey = null,
        string? name = null,
        string source = "source",
        long? size = null,
        string? mediaType = null,
        FileBrowserItemCategory category = FileBrowserItemCategory.Document,
        string? displayPath = null,
        DateTimeOffset? modifiedAt = null,
        string? owner = null,
        FileBrowserContentIdentity? contentIdentity = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new(
            Key(value, source),
            parentKey,
            name ?? value,
            FileBrowserItemKind.File,
            category,
            displayPath,
            FileBrowserChildState.Empty,
            size: size,
            mediaType: mediaType,
            owner: owner,
            modifiedAt: modifiedAt,
            contentIdentity: contentIdentity,
            capabilities: FileBrowserItemCapabilities.Select
                | FileBrowserItemCapabilities.Open
                | FileBrowserItemCapabilities.DownloadFile,
            metadata: metadata);

    public static FileBrowserSourceDescriptor Descriptor(
        string source = "source",
        string? displayName = null,
        FileBrowserSourceCapabilities capabilities = FileBrowserSourceCapabilities.PagedBrowse,
        int recommendedPageSize = 2,
        int maximumPageSize = 100,
        IEnumerable<FileBrowserSortField>? sortFields = null,
        IEnumerable<FileBrowserSearchScope>? searchScopes = null)
        => new(
            Source(source),
            displayName ?? source,
            capabilities: capabilities,
            recommendedPageSize: recommendedPageSize,
            maximumPageSize: maximumPageSize,
            supportedSortFields: sortFields,
            supportedSearchScopes: searchScopes);

    public static FileBrowserBrowseRequest BrowseRequest(
        FileBrowserItemKey parentKey,
        int pageSize = 2,
        string? continuationToken = null,
        FileBrowserSortDescriptor? sort = null,
        FileBrowserFilter? filter = null,
        bool includeDescendants = false,
        string? consistencyToken = null,
        FileBrowserMetadataRequest? metadata = null)
        => new(
            parentKey,
            pageSize,
            continuationToken,
            sort,
            filter,
            includeDescendants,
            consistencyToken,
            metadata);
}

