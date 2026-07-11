namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>Immutable constructor-based composition surface consumed by hosts and later DI adapters.</summary>
public sealed class FileInteractionCoreComposition
{
    internal FileInteractionCoreComposition(
        IEnumerable<FileInteractionProfileDescriptor> profiles,
        IEnumerable<IFileEditHistoryProviderFactory> historyFactories)
    {
        Profiles = new FileInteractionProfileCatalog(profiles);
        HistoryProviders = new FileEditHistoryProviderCatalog(historyFactories);
    }

    public FileInteractionProfileCatalog Profiles { get; }

    public FileEditHistoryProviderCatalog HistoryProviders { get; }
}

/// <summary>
/// Collects explicit profile and history contributions without service location or framework dependencies.
/// Each built composition owns immutable catalog snapshots.
/// </summary>
public sealed class FileInteractionCoreBuilder
{
    private readonly List<FileInteractionProfileDescriptor> profiles = [];
    private readonly List<IFileEditHistoryProviderFactory> historyFactories = [];

    public FileInteractionCoreBuilder()
    {
    }

    public FileInteractionCoreBuilder(
        IEnumerable<FileInteractionProfileDescriptor> profiles,
        IEnumerable<IFileEditHistoryProviderFactory> historyFactories)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(historyFactories);
        foreach (var profile in profiles)
        {
            AddProfile(profile);
        }

        foreach (var factory in historyFactories)
        {
            AddHistoryFactory(factory);
        }
    }

    public FileInteractionCoreBuilder AddProfile(FileInteractionProfileDescriptor profile)
    {
        profiles.Add(profile ?? throw new ArgumentNullException(nameof(profile)));
        return this;
    }

    public FileInteractionCoreBuilder AddHistoryFactory(IFileEditHistoryProviderFactory factory)
    {
        historyFactories.Add(factory ?? throw new ArgumentNullException(nameof(factory)));
        return this;
    }

    public FileInteractionCoreComposition Build()
        => new(profiles, historyFactories);
}
