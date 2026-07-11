using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components;

public enum FileInteractionContentKind
{
    Text,
    Binary
}

public enum FileInteractionContentRequirement
{
    FullContent,
    MetadataOnly
}

/// <summary>The common parameter contract implemented by explicitly registered renderer components.</summary>
public interface IFileInteractionRendererComponent : IComponent
{
    FileInteractionRenderContext Context { get; set; }
}

public sealed record FileInteractionRendererDescriptor
{
    public FileInteractionRendererDescriptor(
        string id,
        string profileId,
        FileInteractionMode mode,
        Type componentType,
        FileInteractionContentKind contentKind,
        int priority = 0,
        FileInteractionContentRequirement contentRequirement = FileInteractionContentRequirement.FullContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(componentType);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (!Enum.IsDefined(contentKind))
        {
            throw new ArgumentOutOfRangeException(nameof(contentKind));
        }

        if (!Enum.IsDefined(contentRequirement))
        {
            throw new ArgumentOutOfRangeException(nameof(contentRequirement));
        }

        if (mode == FileInteractionMode.Edit
            && contentRequirement == FileInteractionContentRequirement.MetadataOnly)
        {
            throw new ArgumentException(
                "Editor renderers require bounded file content; metadata-only editing is not supported.",
                nameof(contentRequirement));
        }

        if (!typeof(IFileInteractionRendererComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException(
                $"Renderer type '{componentType.FullName}' must implement {nameof(IFileInteractionRendererComponent)}.",
                nameof(componentType));
        }

        var contextParameter = componentType.GetProperty(nameof(IFileInteractionRendererComponent.Context));
        if (contextParameter is null
            || !contextParameter.CanWrite
            || contextParameter.GetCustomAttributes(typeof(ParameterAttribute), inherit: true).Length == 0)
        {
            throw new ArgumentException(
                $"Renderer type '{componentType.FullName}' must expose Context as a writable Blazor parameter.",
                nameof(componentType));
        }

        Id = id.Trim();
        ProfileId = profileId.Trim();
        Mode = mode;
        ComponentType = componentType;
        ContentKind = contentKind;
        ContentRequirement = contentRequirement;
        Priority = priority;
    }

    public string Id { get; }

    public string ProfileId { get; }

    public FileInteractionMode Mode { get; }

    public Type ComponentType { get; }

    public FileInteractionContentKind ContentKind { get; }

    public FileInteractionContentRequirement ContentRequirement { get; }

    public int Priority { get; }
}

public enum FileInteractionRendererResolutionStatus
{
    Resolved,
    Unsupported,
    Ambiguous
}

public sealed class FileInteractionRendererResolution
{
    internal FileInteractionRendererResolution(
        FileInteractionRendererResolutionStatus status,
        FileInteractionRendererDescriptor? renderer,
        IEnumerable<FileInteractionRendererDescriptor> candidates)
    {
        Status = status;
        Renderer = renderer;
        Candidates = Array.AsReadOnly(candidates.ToArray());
    }

    public FileInteractionRendererResolutionStatus Status { get; }

    public FileInteractionRendererDescriptor? Renderer { get; }

    public IReadOnlyList<FileInteractionRendererDescriptor> Candidates { get; }

    public bool IsResolved => Status == FileInteractionRendererResolutionStatus.Resolved;
}

/// <summary>Immutable, deterministic registry for renderer components contributed explicitly by a host.</summary>
public sealed class FileInteractionRendererRegistry
{
    private readonly IReadOnlyList<FileInteractionRendererDescriptor> renderers;

    public FileInteractionRendererRegistry(IEnumerable<FileInteractionRendererDescriptor> renderers)
    {
        ArgumentNullException.ThrowIfNull(renderers);
        var values = renderers
            .Select(renderer => renderer ?? throw new ArgumentException(
                "Renderers cannot contain null values.", nameof(renderers)))
            .ToArray();
        var duplicateId = values
            .GroupBy(renderer => renderer.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new ArgumentException(
                $"The renderer id '{duplicateId}' is registered more than once.", nameof(renderers));
        }

        this.renderers = Array.AsReadOnly(values);
    }

    public IReadOnlyList<FileInteractionRendererDescriptor> Renderers => renderers;

    public FileInteractionRendererResolution Resolve(string profileId, FileInteractionMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        var candidates = renderers
            .Where(renderer => string.Equals(renderer.ProfileId, profileId.Trim(), StringComparison.Ordinal))
            .Where(renderer => renderer.Mode == mode)
            .ToArray();
        if (candidates.Length == 0)
        {
            return new FileInteractionRendererResolution(
                FileInteractionRendererResolutionStatus.Unsupported, null, []);
        }

        var priority = candidates.Max(renderer => renderer.Priority);
        var finalists = candidates
            .Where(renderer => renderer.Priority == priority)
            .OrderBy(renderer => renderer.Id, StringComparer.Ordinal)
            .ToArray();
        return finalists.Length == 1
            ? new FileInteractionRendererResolution(
                FileInteractionRendererResolutionStatus.Resolved, finalists[0], finalists)
            : new FileInteractionRendererResolution(
                FileInteractionRendererResolutionStatus.Ambiguous, null, finalists);
    }
}
