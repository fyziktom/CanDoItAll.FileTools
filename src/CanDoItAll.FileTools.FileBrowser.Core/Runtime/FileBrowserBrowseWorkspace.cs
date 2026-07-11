namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Mutable browse-mode state shared by the runtime's focused collaborators.</summary>
internal sealed class FileBrowserBrowseWorkspace(FileBrowserSortDescriptor defaultSort)
{
    public IFileBrowserProvider? Provider { get; set; }

    public FileBrowserLoadedContainer? Container { get; set; }

    public FileBrowserSortDescriptor Sort { get; set; } = defaultSort;

    public FileBrowserFilter Filter { get; set; } = FileBrowserFilter.None;

    public bool IncludeDescendants { get; set; }

    public bool ActiveInvalidated { get; set; }

    public bool SearchInvalidated { get; set; }

    public void Reset(FileBrowserSortDescriptor sort)
    {
        Provider = null;
        Container = null;
        Sort = sort;
        Filter = FileBrowserFilter.None;
        IncludeDescendants = false;
        ActiveInvalidated = false;
        SearchInvalidated = false;
    }
}
