using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Requests a bounded set of metadata from a provider.</summary>
public sealed record FileBrowserMetadataRequest(
    FileBrowserMetadataFields Fields,
    bool IncludeExpensive = false)
{
    public static FileBrowserMetadataRequest Standard { get; } = new(FileBrowserMetadataFields.Standard);
}

/// <summary>Defines deterministic item ordering.</summary>
public sealed record FileBrowserSortDescriptor(
    FileBrowserSortField Field = FileBrowserSortField.Name,
    FileBrowserSortDirection Direction = FileBrowserSortDirection.Ascending,
    bool FoldersFirst = true);

/// <summary>Defines provider-neutral direct-item filtering.</summary>
public sealed record FileBrowserFilter
{
    public static FileBrowserFilter None { get; } = new();

    public FileBrowserFilter(
        IEnumerable<FileBrowserItemKind>? kinds = null,
        IEnumerable<FileBrowserItemCategory>? categories = null,
        IEnumerable<string>? extensions = null,
        string? mediaTypePrefix = null)
    {
        Kinds = new HashSet<FileBrowserItemKind>(kinds ?? []);
        Categories = new HashSet<FileBrowserItemCategory>(categories ?? []);
        Extensions = new HashSet<string>(
            (extensions ?? []).Select(NormalizeExtension),
            StringComparer.OrdinalIgnoreCase);
        MediaTypePrefix = string.IsNullOrWhiteSpace(mediaTypePrefix)
            ? null
            : mediaTypePrefix.Trim();
    }

    public IReadOnlySet<FileBrowserItemKind> Kinds { get; }

    public IReadOnlySet<FileBrowserItemCategory> Categories { get; }

    public IReadOnlySet<string> Extensions { get; }

    public string? MediaTypePrefix { get; }

    public bool IsEmpty => Kinds.Count == 0
        && Categories.Count == 0
        && Extensions.Count == 0
        && MediaTypePrefix is null;

    public bool Matches(FileBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Kinds.Count > 0 && !Kinds.Contains(item.Kind))
        {
            return false;
        }

        if (Categories.Count > 0 && !Categories.Contains(item.Category))
        {
            return false;
        }

        if (Extensions.Count > 0 && item.Kind == FileBrowserItemKind.File)
        {
            var extension = NormalizeExtension(Path.GetExtension(item.Name));
            if (!Extensions.Contains(extension))
            {
                return false;
            }
        }

        return MediaTypePrefix is null
            || item.MediaType?.StartsWith(MediaTypePrefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string NormalizeExtension(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        return trimmed.StartsWith('.') ? trimmed.ToLowerInvariant() : $".{trimmed.ToLowerInvariant()}";
    }
}

/// <summary>A shallow, paged browse request for one container.</summary>
public sealed record FileBrowserBrowseRequest
{
    public FileBrowserBrowseRequest(
        FileBrowserItemKey parentKey,
        int pageSize = 50,
        string? continuationToken = null,
        FileBrowserSortDescriptor? sort = null,
        FileBrowserFilter? filter = null,
        bool includeDescendants = false,
        string? consistencyToken = null,
        FileBrowserMetadataRequest? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(parentKey.Value))
        {
            throw new ArgumentException("A valid parent key is required.", nameof(parentKey));
        }

        if (pageSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        ParentKey = parentKey;
        PageSize = pageSize;
        ContinuationToken = string.IsNullOrWhiteSpace(continuationToken) ? null : continuationToken;
        Sort = sort ?? new FileBrowserSortDescriptor();
        Filter = filter ?? FileBrowserFilter.None;
        IncludeDescendants = includeDescendants;
        ConsistencyToken = string.IsNullOrWhiteSpace(consistencyToken) ? null : consistencyToken;
        Metadata = metadata ?? FileBrowserMetadataRequest.Standard;
    }

    public FileBrowserItemKey ParentKey { get; }

    public int PageSize { get; }

    public string? ContinuationToken { get; }

    public FileBrowserSortDescriptor Sort { get; }

    public FileBrowserFilter Filter { get; }

    public bool IncludeDescendants { get; }

    public string? ConsistencyToken { get; }

    public FileBrowserMetadataRequest Metadata { get; }

    public FileBrowserBrowseRequest Next(string continuationToken, string? consistencyToken = null)
        => new(
            ParentKey,
            PageSize,
            continuationToken,
            Sort,
            Filter,
            IncludeDescendants,
            consistencyToken ?? ConsistencyToken,
            Metadata);

    public FileBrowserBrowseRequest FirstPage()
        => new(ParentKey, PageSize, null, Sort, Filter, IncludeDescendants, ConsistencyToken, Metadata);
}

/// <summary>Identifies a browse query independently from its continuation page.</summary>
public readonly record struct FileBrowserQueryFingerprint
{
    public FileBrowserQueryFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public static FileBrowserQueryFingerprint From(FileBrowserBrowseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var raw = string.Join('|',
            request.ParentKey,
            request.PageSize.ToString(CultureInfo.InvariantCulture),
            request.Sort.Field,
            request.Sort.Direction,
            request.Sort.FoldersFirst,
            request.IncludeDescendants,
            request.Filter.MediaTypePrefix ?? string.Empty,
            string.Join(',', request.Filter.Kinds.Order()),
            string.Join(',', request.Filter.Categories.Order()),
            string.Join(',', request.Filter.Extensions.Order(StringComparer.OrdinalIgnoreCase)),
            request.Metadata.Fields,
            request.Metadata.IncludeExpensive);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return new FileBrowserQueryFingerprint(Convert.ToHexString(hash));
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Budget for an explicit progressive hierarchy search.</summary>
public sealed record FileBrowserSearchBudget
{
    public FileBrowserSearchBudget(
        int maximumContainers = 250,
        int maximumItems = 10_000,
        TimeSpan? maximumDuration = null,
        int maximumConcurrentRequests = 1,
        int? maximumMatches = null,
        long maximumRetainedBytes = 8L * 1024 * 1024)
    {
        if (maximumContainers is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumContainers));
        }

        if (maximumItems is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }

        MaximumDuration = maximumDuration ?? TimeSpan.FromSeconds(30);
        if (MaximumDuration < TimeSpan.FromMilliseconds(10) || MaximumDuration > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }

        if (maximumConcurrentRequests is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrentRequests));
        }

        int effectiveMaximumMatches = maximumMatches ?? Math.Min(1_000, maximumItems);
        if (effectiveMaximumMatches < 1 || effectiveMaximumMatches > maximumItems)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMatches));
        }

        if (maximumRetainedBytes is < 1 or > 256L * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRetainedBytes));
        }

        MaximumContainers = maximumContainers;
        MaximumItems = maximumItems;
        MaximumConcurrentRequests = maximumConcurrentRequests;
        MaximumMatches = effectiveMaximumMatches;
        MaximumRetainedBytes = maximumRetainedBytes;
    }

    public int MaximumContainers { get; }

    public int MaximumItems { get; }

    public TimeSpan MaximumDuration { get; }

    public int MaximumConcurrentRequests { get; }

    public int MaximumMatches { get; }

    public long MaximumRetainedBytes { get; }
}

/// <summary>A search request against a browser source or loaded tree.</summary>
public sealed record FileBrowserSearchRequest
{
    public FileBrowserSearchRequest(
        FileBrowserItemKey containerKey,
        string query,
        FileBrowserSearchScope scope,
        int pageSize = 50,
        string? continuationToken = null,
        FileBrowserSortDescriptor? sort = null,
        FileBrowserFilter? filter = null,
        FileBrowserSearchBudget? budget = null,
        string? consistencyToken = null,
        FileBrowserMetadataRequest? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (string.IsNullOrWhiteSpace(containerKey.Value))
        {
            throw new ArgumentException("A valid container key is required.", nameof(containerKey));
        }

        if (pageSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        ContainerKey = containerKey;
        Query = query.Trim();
        Scope = scope;
        PageSize = pageSize;
        ContinuationToken = string.IsNullOrWhiteSpace(continuationToken) ? null : continuationToken;
        Sort = sort ?? new FileBrowserSortDescriptor();
        Filter = filter ?? FileBrowserFilter.None;
        Budget = budget ?? new FileBrowserSearchBudget();
        ConsistencyToken = string.IsNullOrWhiteSpace(consistencyToken) ? null : consistencyToken;
        Metadata = metadata ?? FileBrowserMetadataRequest.Standard;
    }

    public FileBrowserItemKey ContainerKey { get; }

    public string Query { get; }

    public FileBrowserSearchScope Scope { get; }

    public int PageSize { get; }

    public string? ContinuationToken { get; }

    public FileBrowserSortDescriptor Sort { get; }

    public FileBrowserFilter Filter { get; }

    public FileBrowserSearchBudget Budget { get; }

    public string? ConsistencyToken { get; }

    public FileBrowserMetadataRequest Metadata { get; }

    public FileBrowserSearchRequest Next(string continuationToken, string? consistencyToken = null)
        => new(ContainerKey, Query, Scope, PageSize, continuationToken, Sort, Filter, Budget, consistencyToken ?? ConsistencyToken, Metadata);
}
