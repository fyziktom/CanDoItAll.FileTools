namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Provides a shallow, paged hierarchy of file-like occurrences.</summary>
public interface IFileBrowserProvider
{
    FileBrowserSourceDescriptor Descriptor { get; }

    ValueTask<FileBrowserItem> GetRootAsync(
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
        FileBrowserItemKey itemKey,
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default);

    ValueTask<FileBrowserPage> BrowseAsync(
        FileBrowserBrowseRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional native/indexed search implemented by capable sources.</summary>
public interface IFileBrowserSearchProvider
{
    ValueTask<FileBrowserSearchPage> SearchAsync(
        FileBrowserSearchRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional range-aware content access implemented by capable sources.</summary>
public interface IFileBrowserContentProvider
{
    ValueTask<FileBrowserContentLease> OpenReadAsync(
        FileBrowserReadRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional provider-specific action discovery and execution.</summary>
public interface IFileBrowserActionProvider
{
    ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default);

    ValueTask<FileBrowserActionResult> ExecuteAsync(
        FileBrowserActionRequest request,
        CancellationToken cancellationToken = default);
}
