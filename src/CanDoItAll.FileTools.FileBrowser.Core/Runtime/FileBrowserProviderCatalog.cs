namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Read-only provider lookup used by a browser session.</summary>
public interface IFileBrowserProviderCatalog
{
    IReadOnlyList<FileBrowserSourceDescriptor> Sources { get; }

    IFileBrowserProvider Get(FileBrowserSourceId sourceId);

    bool TryGet(FileBrowserSourceId sourceId, out IFileBrowserProvider? provider);
}

/// <summary>Validates and indexes provider instances without service location.</summary>
public sealed class FileBrowserProviderCatalog : IFileBrowserProviderCatalog
{
    private readonly IReadOnlyDictionary<FileBrowserSourceId, IFileBrowserProvider> providers;

    public FileBrowserProviderCatalog(IEnumerable<IFileBrowserProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var values = providers.ToArray();
        if (values.Length == 0)
        {
            throw new ArgumentException("At least one file browser provider is required.", nameof(providers));
        }

        var duplicate = values.GroupBy(provider => provider.Descriptor.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Source identifier '{duplicate.Key}' is registered more than once.", nameof(providers));
        }

        this.providers = values.ToDictionary(provider => provider.Descriptor.Id);
        Sources = values
            .Select(provider => provider.Descriptor)
            .OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<FileBrowserSourceDescriptor> Sources { get; }

    public IFileBrowserProvider Get(FileBrowserSourceId sourceId)
        => providers.TryGetValue(sourceId, out var provider)
            ? provider
            : throw new KeyNotFoundException($"File browser source '{sourceId}' is not registered.");

    public bool TryGet(FileBrowserSourceId sourceId, out IFileBrowserProvider? provider)
        => providers.TryGetValue(sourceId, out provider);
}

