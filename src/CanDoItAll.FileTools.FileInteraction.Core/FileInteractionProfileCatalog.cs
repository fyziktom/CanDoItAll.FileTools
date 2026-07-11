using System.Collections.ObjectModel;

namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>Describes the specificity that selected a file interaction profile.</summary>
public enum FileInteractionMatchKind
{
    Fallback = 1,
    Extension = 2,
    MediaTypeWildcard = 3,
    MediaTypeExact = 4
}

/// <summary>Describes whether profile resolution succeeded.</summary>
public enum FileInteractionResolutionStatus
{
    Resolved,
    Unsupported,
    Ambiguous
}

/// <summary>A scored profile candidate produced by the deterministic resolver.</summary>
public sealed record FileInteractionProfileMatch(
    FileInteractionProfileDescriptor Profile,
    FileInteractionMatchKind MatchKind);

/// <summary>The explicit result of profile resolution, including unsupported and ambiguous outcomes.</summary>
public sealed class FileInteractionResolution
{
    internal FileInteractionResolution(
        FileInteractionResolutionStatus status,
        FileInteractionProfileDescriptor? profile,
        IEnumerable<FileInteractionProfileMatch> candidates)
    {
        Status = status;
        Profile = profile;
        Candidates = Array.AsReadOnly(candidates.ToArray());
    }

    public FileInteractionResolutionStatus Status { get; }

    public FileInteractionProfileDescriptor? Profile { get; }

    public IReadOnlyList<FileInteractionProfileMatch> Candidates { get; }

    public bool IsResolved => Status == FileInteractionResolutionStatus.Resolved;
}

/// <summary>
/// Immutable catalog that resolves profiles by match specificity and then priority.
/// Exact media types outrank media wildcards, which outrank exact extensions and fallbacks.
/// </summary>
public sealed class FileInteractionProfileCatalog
{
    private readonly IReadOnlyList<FileInteractionProfileDescriptor> profiles;

    public FileInteractionProfileCatalog(IEnumerable<FileInteractionProfileDescriptor> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var values = profiles.Select(profile => profile ?? throw new ArgumentException("Profiles cannot contain null values.", nameof(profiles)))
            .ToArray();
        var duplicateId = values
            .GroupBy(profile => profile.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new ArgumentException($"The profile id '{duplicateId}' is registered more than once.", nameof(profiles));
        }

        this.profiles = Array.AsReadOnly(values);
    }

    public IReadOnlyList<FileInteractionProfileDescriptor> Profiles => profiles;

    public FileInteractionResolution Resolve(
        FileInteractionRequest request,
        FileInteractionCapabilities requiredCapabilities = FileInteractionCapabilities.None)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidates = profiles
            .Where(profile => profile.Supports(request.Mode))
            .Where(profile => (profile.Capabilities & requiredCapabilities) == requiredCapabilities)
            .Select(profile => TryMatch(profile, request))
            .Where(match => match is not null)
            .Cast<FileInteractionProfileMatch>()
            .ToArray();

        if (candidates.Length == 0)
        {
            return new FileInteractionResolution(
                FileInteractionResolutionStatus.Unsupported,
                null,
                []);
        }

        var bestKind = candidates.Max(match => match.MatchKind);
        var bestPriority = candidates
            .Where(match => match.MatchKind == bestKind)
            .Max(match => match.Profile.Priority);
        var finalists = candidates
            .Where(match => match.MatchKind == bestKind && match.Profile.Priority == bestPriority)
            .OrderBy(match => match.Profile.Id, StringComparer.Ordinal)
            .ToArray();

        return finalists.Length == 1
            ? new FileInteractionResolution(
                FileInteractionResolutionStatus.Resolved,
                finalists[0].Profile,
                finalists)
            : new FileInteractionResolution(
                FileInteractionResolutionStatus.Ambiguous,
                null,
                finalists);
    }

    private static FileInteractionProfileMatch? TryMatch(
        FileInteractionProfileDescriptor profile,
        FileInteractionRequest request)
    {
        if (request.MediaType is not null
            && profile.MediaTypes.Contains(request.MediaType, StringComparer.Ordinal))
        {
            return new FileInteractionProfileMatch(profile, FileInteractionMatchKind.MediaTypeExact);
        }

        if (request.MediaType is not null)
        {
            var separator = request.MediaType.IndexOf('/');
            var wildcard = separator > 0 ? $"{request.MediaType[..separator]}/*" : null;
            if (wildcard is not null && profile.MediaTypes.Contains(wildcard, StringComparer.Ordinal))
            {
                return new FileInteractionProfileMatch(profile, FileInteractionMatchKind.MediaTypeWildcard);
            }
        }

        if (request.Extension.Length > 0
            && profile.Extensions.Contains(request.Extension, StringComparer.Ordinal))
        {
            return new FileInteractionProfileMatch(profile, FileInteractionMatchKind.Extension);
        }

        return profile.Extensions.Contains("*", StringComparer.Ordinal)
            || profile.MediaTypes.Contains("*/*", StringComparer.Ordinal)
                ? new FileInteractionProfileMatch(profile, FileInteractionMatchKind.Fallback)
                : null;
    }
}
