namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>A replaceable provider catalog identified by a host-owned opaque revision.</summary>
public sealed class FileBrowserSourceSet
{
    private readonly IReadOnlyDictionary<FileBrowserSourceId, IFileBrowserProvider> providers;

    public FileBrowserSourceSet(string revision, IEnumerable<IFileBrowserProvider> providers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        ArgumentNullException.ThrowIfNull(providers);
        IFileBrowserProvider[] values = providers.ToArray();
        if (values.Any(provider => provider is null))
        {
            throw new ArgumentException("A source set cannot contain a null provider.", nameof(providers));
        }

        IGrouping<FileBrowserSourceId, IFileBrowserProvider>? duplicate = values
            .GroupBy(provider => provider.Descriptor.Id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Source identifier '{duplicate.Key}' is registered more than once.",
                nameof(providers));
        }

        Revision = revision.Trim();
        this.providers = values.ToDictionary(provider => provider.Descriptor.Id);
        Sources = values
            .Select(provider => provider.Descriptor)
            .OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public string Revision { get; }

    public IReadOnlyList<FileBrowserSourceDescriptor> Sources { get; }

    public IFileBrowserProvider Get(FileBrowserSourceId sourceId)
        => providers.TryGetValue(sourceId, out IFileBrowserProvider? provider)
            ? provider
            : throw new KeyNotFoundException($"File browser source '{sourceId}' is not registered.");

    public bool TryGet(FileBrowserSourceId sourceId, out IFileBrowserProvider? provider)
        => providers.TryGetValue(sourceId, out provider);
}
