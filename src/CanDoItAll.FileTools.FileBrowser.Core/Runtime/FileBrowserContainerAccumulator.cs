namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>
/// Owns the active container's page accumulation independently from reusable retention.
/// Instances are cloned before an append so cancellation or validation failures cannot mutate
/// the currently rendered result.
/// </summary>
public sealed class FileBrowserContainerAccumulator
{
    private readonly List<FileBrowserItemKey> itemOrder = [];
    private readonly Dictionary<FileBrowserItemKey, FileBrowserItem> items = [];
    private readonly HashSet<string> observedContinuationTokens = new(StringComparer.Ordinal);

    private FileBrowserContainerAccumulator(FileBrowserContainerQueryKey queryKey)
    {
        QueryKey = queryKey;
    }

    public FileBrowserContainerQueryKey QueryKey { get; }

    public string? NextContinuationToken { get; private set; }

    public long? TotalCount { get; private set; }

    public string? ConsistencyToken { get; private set; }

    public FileBrowserCompleteness Completeness { get; private set; } = FileBrowserCompleteness.Unknown;

    public IReadOnlyList<FileBrowserPageWarning> Warnings { get; private set; } = [];

    public FileBrowserError? Error { get; private set; }

    public int LoadedPageCount { get; private set; }

    public DateTimeOffset LastAccessedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static FileBrowserContainerAccumulator Start(
        FileBrowserBrowseRequest request,
        FileBrowserPage page)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(page);
        var accumulator = new FileBrowserContainerAccumulator(CreateQueryKey(request));
        accumulator.ApplyPage(request, page, FileBrowserPageApplyMode.Replace);
        return accumulator;
    }

    public static FileBrowserContainerAccumulator FromSnapshot(FileBrowserContainerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var accumulator = new FileBrowserContainerAccumulator(snapshot.QueryKey)
        {
            NextContinuationToken = snapshot.NextContinuationToken,
            TotalCount = snapshot.TotalCount,
            ConsistencyToken = snapshot.ConsistencyToken,
            Completeness = snapshot.Completeness,
            Warnings = snapshot.Warnings.ToArray(),
            Error = snapshot.Error,
            LoadedPageCount = snapshot.LoadedPageCount,
            LastAccessedAt = snapshot.LastAccessedAt
        };
        foreach (FileBrowserItem item in snapshot.Items)
        {
            accumulator.Upsert(item);
        }

        accumulator.observedContinuationTokens.UnionWith(snapshot.ContinuationHistory.Tokens);
        if (accumulator.observedContinuationTokens.Count == 0
            && snapshot.NextContinuationToken is not null)
        {
            accumulator.observedContinuationTokens.Add(snapshot.NextContinuationToken);
        }

        return accumulator;
    }

    public FileBrowserContainerAccumulator Clone()
    {
        var clone = FromSnapshot(Snapshot());
        clone.observedContinuationTokens.Clear();
        clone.observedContinuationTokens.UnionWith(observedContinuationTokens);
        return clone;
    }

    public void ApplyPage(
        FileBrowserBrowseRequest request,
        FileBrowserPage page,
        FileBrowserPageApplyMode mode)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(page);
        FileBrowserProviderResponseValidator.ValidateBrowsePage(request, page);
        if (CreateQueryKey(request) != QueryKey)
        {
            throw new InvalidOperationException("A page cannot be applied to another container query.");
        }

        if (mode == FileBrowserPageApplyMode.Append)
        {
            ValidateAppend(request, page);
        }

        var nextOrder = mode == FileBrowserPageApplyMode.Replace
            ? new List<FileBrowserItemKey>()
            : itemOrder.ToList();
        var nextItems = mode == FileBrowserPageApplyMode.Replace
            ? new Dictionary<FileBrowserItemKey, FileBrowserItem>()
            : new Dictionary<FileBrowserItemKey, FileBrowserItem>(items);
        foreach (FileBrowserItem item in page.Items)
        {
            if (!nextItems.ContainsKey(item.Key))
            {
                nextOrder.Add(item.Key);
            }

            nextItems[item.Key] = item;
        }

        long? totalCount = mode == FileBrowserPageApplyMode.Append
            ? MergeTotalCount(TotalCount, page.TotalCount, nextItems.Count)
            : page.TotalCount;

        itemOrder.Clear();
        itemOrder.AddRange(nextOrder);
        items.Clear();
        foreach ((FileBrowserItemKey key, FileBrowserItem item) in nextItems)
        {
            items.Add(key, item);
        }

        if (mode == FileBrowserPageApplyMode.Replace)
        {
            observedContinuationTokens.Clear();
            LoadedPageCount = 0;
            Warnings = page.Warnings.ToArray();
            Completeness = page.Completeness;
        }
        else
        {
            Warnings = FileBrowserProviderResponseValidator.MergeWarnings(Warnings, page.Warnings);
            Completeness = FileBrowserProviderResponseValidator.MergeCompleteness(
                Completeness,
                page.Completeness);
        }

        NextContinuationToken = page.NextContinuationToken;
        TotalCount = totalCount;
        ConsistencyToken = page.ConsistencyToken ?? request.ConsistencyToken ?? ConsistencyToken;
        if (page.NextContinuationToken is not null)
        {
            observedContinuationTokens.Add(page.NextContinuationToken);
        }

        Error = null;
        LoadedPageCount++;
        LastAccessedAt = DateTimeOffset.UtcNow;
    }

    public FileBrowserContainerSnapshot Snapshot()
        => new FileBrowserContainerSnapshot(
            QueryKey,
            Array.AsReadOnly(itemOrder.Select(key => items[key]).ToArray()),
            NextContinuationToken,
            TotalCount,
            ConsistencyToken,
            Completeness,
            Array.AsReadOnly(Warnings.ToArray()),
            Error,
            LoadedPageCount,
            LastAccessedAt)
        {
            ContinuationHistory = FileBrowserContinuationHistory.Create(observedContinuationTokens)
        };

    private static FileBrowserContainerQueryKey CreateQueryKey(FileBrowserBrowseRequest request)
        => new(request.ParentKey, FileBrowserQueryFingerprint.From(request));

    private void ValidateAppend(FileBrowserBrowseRequest request, FileBrowserPage page)
    {
        if (request.ContinuationToken is null)
        {
            throw new InvalidOperationException("Appending requires a continuation token.");
        }

        if (LoadedPageCount == 0
            || !string.Equals(NextContinuationToken, request.ContinuationToken, StringComparison.Ordinal))
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.StaleCursor,
                "The continuation token no longer matches the active folder state.",
                isRetryable: true));
        }

        if (ConsistencyToken is not null
            && page.ConsistencyToken is not null
            && !string.Equals(ConsistencyToken, page.ConsistencyToken, StringComparison.Ordinal))
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.StaleCursor,
                "The source revision changed while loading this folder.",
                isRetryable: true));
        }

        FileBrowserProviderResponseValidator.ValidateCursorNotPreviouslyObserved(
            page.NextContinuationToken,
            observedContinuationTokens);
    }

    private static long? MergeTotalCount(long? existing, long? incoming, int itemCount)
    {
        if (incoming.HasValue && incoming.Value < itemCount)
        {
            throw FileBrowserProviderResponseValidator.CorruptProviderResponse(
                "The provider returned a total count smaller than the accumulated result.");
        }

        if (existing.HasValue && incoming.HasValue && existing.Value != incoming.Value)
        {
            throw FileBrowserProviderResponseValidator.CorruptProviderResponse(
                "The provider changed the total count while paging a stable result.");
        }

        return incoming ?? existing;
    }

    private void Upsert(FileBrowserItem item)
    {
        if (!items.ContainsKey(item.Key))
        {
            itemOrder.Add(item.Key);
        }

        items[item.Key] = item;
    }
}
