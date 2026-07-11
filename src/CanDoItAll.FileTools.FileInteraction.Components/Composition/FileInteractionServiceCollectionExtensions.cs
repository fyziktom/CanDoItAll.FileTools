using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.FileTools.FileInteraction.Components;

public static class FileInteractionServiceCollectionExtensions
{
    /// <summary>
    /// Registers one explicitly built immutable composition. Hosts choose base and optional renderer contributions.
    /// </summary>
    public static IServiceCollection AddFileInteractionComponents(
        this IServiceCollection services,
        Action<FileInteractionComponentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new FileInteractionComponentBuilder();
        configure(builder);
        services.AddSingleton<FileInteractionComponentComposition>(builder.Build());
        return services;
    }
}
