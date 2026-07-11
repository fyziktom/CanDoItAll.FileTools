namespace CanDoItAll.FileTools.FileInteraction.Components;

public sealed class FileInteractionComponentComposition
{
    private static readonly Lazy<FileInteractionComponentComposition> builtIn =
        new(() => new FileInteractionComponentBuilder().AddBuiltIns().Build());

    internal FileInteractionComponentComposition(
        FileInteractionCoreComposition core,
        FileInteractionRendererRegistry renderers)
    {
        Core = core;
        Renderers = renderers;
    }

    public FileInteractionCoreComposition Core { get; }

    public FileInteractionRendererRegistry Renderers { get; }

    /// <summary>A dependency-light composition containing only the base package renderers.</summary>
    public static FileInteractionComponentComposition BuiltIn => builtIn.Value;
}

/// <summary>Explicit construction surface for profiles, history providers, and optional UI renderers.</summary>
public sealed class FileInteractionComponentBuilder
{
    private readonly FileInteractionCoreBuilder core = new();
    private readonly List<FileInteractionRendererDescriptor> renderers = [];

    public FileInteractionComponentBuilder AddProfile(FileInteractionProfileDescriptor profile)
    {
        core.AddProfile(profile);
        return this;
    }

    public FileInteractionComponentBuilder AddHistoryFactory(IFileEditHistoryProviderFactory factory)
    {
        core.AddHistoryFactory(factory);
        return this;
    }

    public FileInteractionComponentBuilder AddRenderer(FileInteractionRendererDescriptor renderer)
    {
        renderers.Add(renderer ?? throw new ArgumentNullException(nameof(renderer)));
        return this;
    }

    public FileInteractionComponentComposition Build()
    {
        var coreComposition = core.Build();
        var knownProfileIds = coreComposition.Profiles.Profiles
            .Select(profile => profile.Id)
            .ToHashSet(StringComparer.Ordinal);
        var unknown = renderers.FirstOrDefault(renderer => !knownProfileIds.Contains(renderer.ProfileId));
        if (unknown is not null)
        {
            throw new InvalidOperationException(
                $"Renderer '{unknown.Id}' refers to unknown profile '{unknown.ProfileId}'.");
        }

        return new FileInteractionComponentComposition(
            coreComposition,
            new FileInteractionRendererRegistry(renderers));
    }
}
