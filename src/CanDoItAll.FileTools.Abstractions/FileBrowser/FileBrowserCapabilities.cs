namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Describes the structural role of a browser item.</summary>
public enum FileBrowserItemKind
{
    /// <summary>A navigable hierarchy node.</summary>
    Container,
    /// <summary>A file-like leaf.</summary>
    File,
    /// <summary>A link-like leaf or provider reference.</summary>
    Link
}

/// <summary>Describes an item's display category.</summary>
public enum FileBrowserItemCategory
{
    Folder,
    Document,
    Image,
    Video,
    Audio,
    Archive,
    Code,
    Data,
    Link,
    Other
}

/// <summary>Distinguishes unknown children from an empty or expandable container.</summary>
public enum FileBrowserChildState
{
    Unknown,
    Empty,
    HasChildren
}

/// <summary>Describes the completeness of a value or response.</summary>
public enum FileBrowserCompleteness
{
    Unknown,
    Partial,
    Complete
}

/// <summary>Metadata fields that a caller can request explicitly.</summary>
[Flags]
public enum FileBrowserMetadataFields
{
    None = 0,
    Name = 1 << 0,
    DisplayPath = 1 << 1,
    Kind = 1 << 2,
    ChildState = 1 << 3,
    Size = 1 << 4,
    MediaType = 1 << 5,
    Owner = 1 << 6,
    Timestamps = 1 << 7,
    ContentIdentity = 1 << 8,
    Links = 1 << 9,
    Custom = 1 << 10,
    Standard = Name | DisplayPath | Kind | ChildState | Size | MediaType | Owner | Timestamps | ContentIdentity | Links,
    All = Standard | Custom
}

/// <summary>Actions that an item can expose without leaking provider SDK types.</summary>
[Flags]
public enum FileBrowserItemCapabilities
{
    None = 0,
    Select = 1 << 0,
    Navigate = 1 << 1,
    /// <summary>
    /// The host may offer primary invocation for this item. This is descriptive eligibility only;
    /// a component or provider must not execute an URI, process, download, or viewer directly.
    /// </summary>
    Open = 1 << 2,
    OpenInNewTab = 1 << 3,
    DownloadFile = 1 << 4,
    DownloadDirectory = 1 << 5,
    CopyPath = 1 << 6,
    CopyContentIdentity = 1 << 7,
    Preview = 1 << 8,
    CustomActions = 1 << 9
}

/// <summary>Capabilities advertised by a configured source.</summary>
[Flags]
public enum FileBrowserSourceCapabilities
{
    None = 0,
    PagedBrowse = 1 << 0,
    RecursiveBrowse = 1 << 1,
    NativeSearch = 1 << 2,
    ContentRead = 1 << 3,
    RangeRead = 1 << 4,
    CustomActions = 1 << 5
}

/// <summary>Fields supported for deterministic sorting.</summary>
public enum FileBrowserSortField
{
    Name,
    ModifiedAt,
    Size,
    Type,
    Owner,
    Path
}

/// <summary>Sort direction.</summary>
public enum FileBrowserSortDirection
{
    Ascending,
    Descending
}

/// <summary>Search algorithms exposed by the core.</summary>
public enum FileBrowserSearchScope
{
    LoadedFolder,
    LoadedDescendants,
    Provider,
    Progressive
}

/// <summary>Stable identifiers for built-in and host actions.</summary>
public static class FileBrowserActionIds
{
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        Open,
        OpenInNewTab,
        Download,
        CopyPath,
        CopyContentIdentity,
        Refresh
    };

    public const string Open = "open";
    public const string OpenInNewTab = "open-new-tab";
    public const string Download = "download";
    public const string CopyPath = "copy-path";
    public const string CopyContentIdentity = "copy-content-id";
    public const string Refresh = "refresh";

    /// <summary>Returns whether an identifier is reserved by the browser's built-in action contract.</summary>
    public static bool IsReserved(string actionId)
        => !string.IsNullOrWhiteSpace(actionId) && Reserved.Contains(actionId.Trim());
}
