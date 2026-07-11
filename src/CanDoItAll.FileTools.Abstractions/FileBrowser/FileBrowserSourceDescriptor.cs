using System.Collections.ObjectModel;

namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Describes a configured browser source and its supported operations.</summary>
public sealed record FileBrowserSourceDescriptor
{
    public FileBrowserSourceDescriptor(
        FileBrowserSourceId id,
        string displayName,
        string icon = "folder",
        string? description = null,
        FileBrowserSourceCapabilities capabilities = FileBrowserSourceCapabilities.PagedBrowse,
        int recommendedPageSize = 50,
        int maximumPageSize = 250,
        IEnumerable<FileBrowserSortField>? supportedSortFields = null,
        IEnumerable<FileBrowserSearchScope>? supportedSearchScopes = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("A source identifier is required.", nameof(id));
        }

        if (recommendedPageSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(recommendedPageSize));
        }

        if (maximumPageSize is < 1 or > 1000 || maximumPageSize < recommendedPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPageSize));
        }

        Id = id;
        DisplayName = displayName.Trim();
        Icon = string.IsNullOrWhiteSpace(icon) ? "folder" : icon.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Capabilities = capabilities;
        RecommendedPageSize = recommendedPageSize;
        MaximumPageSize = maximumPageSize;
        SupportedSortFields = new HashSet<FileBrowserSortField>(
            supportedSortFields ?? Enum.GetValues<FileBrowserSortField>());
        SupportedSearchScopes = new HashSet<FileBrowserSearchScope>(
            supportedSearchScopes ??
            [FileBrowserSearchScope.LoadedFolder, FileBrowserSearchScope.LoadedDescendants, FileBrowserSearchScope.Progressive]);
        Metadata = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));

        if (Capabilities.HasFlag(FileBrowserSourceCapabilities.NativeSearch)
            && !SupportedSearchScopes.Contains(FileBrowserSearchScope.Provider))
        {
            throw new ArgumentException("A native-search source must advertise provider search scope.", nameof(supportedSearchScopes));
        }
    }

    public FileBrowserSourceId Id { get; }

    public string DisplayName { get; }

    public string Icon { get; }

    public string? Description { get; }

    public FileBrowserSourceCapabilities Capabilities { get; }

    public int RecommendedPageSize { get; }

    public int MaximumPageSize { get; }

    public IReadOnlySet<FileBrowserSortField> SupportedSortFields { get; }

    public IReadOnlySet<FileBrowserSearchScope> SupportedSearchScopes { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public bool Supports(FileBrowserSourceCapabilities capability) => Capabilities.HasFlag(capability);
}
