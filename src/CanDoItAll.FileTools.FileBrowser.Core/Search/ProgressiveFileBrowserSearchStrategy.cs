namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>
/// Performs an explicit, bounded breadth-first search by requesting shallow pages. Multi-page
/// results are served from a retained immutable snapshot rather than the browser's evictable tree.
/// </summary>
public sealed class ProgressiveFileBrowserSearchStrategy : IFileBrowserSearchStrategy
{
    private readonly ProgressiveSearchContinuationStore continuations;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates a strategy with bounded, expiring continuation state.</summary>
    /// <param name="maximumRetainedSearches">
    /// Maximum number of incomplete result snapshots retained by this strategy instance.
    /// </param>
    /// <param name="retention">
    /// Absolute lifetime of an incomplete result snapshot. The default is fifteen minutes.
    /// </param>
    /// <param name="timeProvider">Time source used to expire retained snapshots.</param>
    public ProgressiveFileBrowserSearchStrategy(
        int maximumRetainedSearches = 16,
        TimeSpan? retention = null,
        TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
        continuations = new ProgressiveSearchContinuationStore(
            maximumRetainedSearches,
            retention ?? TimeSpan.FromMinutes(15),
            this.timeProvider);
    }

    public string Id => "progressive-breadth-first";

    public FileBrowserSearchScope Scope => FileBrowserSearchScope.Progressive;

    public bool CanSearch(IFileBrowserProvider provider)
        => provider.Descriptor.SupportedSearchScopes.Contains(Scope);

    public async ValueTask<FileBrowserSearchPage> SearchAsync(
        FileBrowserSearchStrategyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var fingerprint = ProgressiveSearchRequestFingerprint.Create(context.Provider, context.Request);
        if (context.Request.ContinuationToken is not null)
        {
            ProgressiveSearchContinuation continuation = continuations.TakeNextPage(
                context.Request.ContinuationToken,
                fingerprint,
                context.Request.ConsistencyToken,
                context.Request.PageSize,
                cancellationToken);
            return new FileBrowserSearchPage(
                continuation.Items,
                Id,
                continuation.NextContinuationToken,
                continuation.TotalCount,
                continuation.IsPartial,
                continuation.ScannedContainers,
                continuation.ScannedItems,
                continuation.ConsistencyToken,
                continuation.Warnings,
                continuation.TotalCount > int.MaxValue
                    ? int.MaxValue
                    : (int)continuation.TotalCount,
                continuation.RetainedBytes,
                continuation.PeakConcurrentRequests,
                continuation.Elapsed);
        }

        ProgressiveSearchSnapshot snapshot = await CaptureSnapshotAsync(context, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var firstPage = snapshot.Matches.Take(context.Request.PageSize).ToArray();
        string? nextContinuationToken = null;
        if (firstPage.Length < snapshot.Matches.Count)
        {
            nextContinuationToken = continuations.Retain(
                snapshot,
                fingerprint,
                firstPage.Length);
        }

        return new FileBrowserSearchPage(
            firstPage,
            Id,
            nextContinuationToken,
            snapshot.Matches.Count,
            snapshot.IsPartial,
            snapshot.ScannedContainers,
            snapshot.ScannedItems,
            snapshot.ConsistencyToken,
            snapshot.Warnings,
            snapshot.Matches.Count,
            snapshot.RetainedBytes,
            snapshot.PeakConcurrentRequests,
            snapshot.Elapsed);
    }

    private async ValueTask<ProgressiveSearchSnapshot> CaptureSnapshotAsync(
        FileBrowserSearchStrategyContext context,
        CancellationToken cancellationToken)
    {
        // The configured traversal budget is exhausted before page one is returned. That makes
        // global ordering deterministic and lets later pages use only a stable result snapshot;
        // no provider cursor or evictable tree node is needed after this method completes. The
        // tradeoff is a slower first page and bounded per-strategy memory until the cursor ends.
        var queue = new Queue<FileBrowserItemKey>();
        var visitedContainers = new HashSet<FileBrowserItemKey>();
        var visitedItems = new HashSet<FileBrowserItemKey>();
        var matches = new List<FileBrowserItem>();
        var warnings = new List<FileBrowserPageWarning>();
        var scannedContainers = 0;
        var scannedItems = 0;
        long retainedBytes = 0;
        var browseRequests = 0;
        var budgetReached = false;
        var rootConsistencyToken = context.Request.ConsistencyToken;
        queue.Enqueue(context.Request.ContainerKey);

        long started = timeProvider.GetTimestamp();
        using var durationCancellation = new CancellationTokenSource(
            context.Request.Budget.MaximumDuration,
            timeProvider);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            durationCancellation.Token);
        CancellationToken operationToken = operationCancellation.Token;
        try
        {
            while (queue.Count > 0 && !budgetReached)
            {
                operationToken.ThrowIfCancellationRequested();
                var containerKey = queue.Dequeue();
                if (!visitedContainers.Add(containerKey))
                {
                    continue;
                }

                if (scannedContainers >= context.Request.Budget.MaximumContainers)
                {
                    budgetReached = true;
                    break;
                }

                scannedContainers++;

                string? continuationToken = null;
                var observedContinuationTokens = new HashSet<string>(StringComparer.Ordinal);
                var containerConsistencyToken = containerKey == context.Request.ContainerKey
                    ? rootConsistencyToken
                    : null;
                do
                {
                    operationToken.ThrowIfCancellationRequested();
                    var browseRequest = new FileBrowserBrowseRequest(
                        containerKey,
                        Math.Min(
                            context.Provider.Descriptor.MaximumPageSize,
                            context.Provider.Descriptor.RecommendedPageSize),
                        continuationToken,
                        context.Request.Sort,
                        FileBrowserFilter.None,
                        includeDescendants: false,
                        containerConsistencyToken,
                        context.Request.Metadata);
                    browseRequests++;
                    var page = await context.Data.BrowseAndCacheAsync(
                        browseRequest,
                        continuationToken is null ? FileBrowserPageApplyMode.Replace : FileBrowserPageApplyMode.Append,
                        operationToken);
                    operationToken.ThrowIfCancellationRequested();
                    FileBrowserProviderResponseValidator.ValidateBrowsePage(browseRequest, page);
                    FileBrowserProviderResponseValidator.ValidateCursorNotPreviouslyObserved(
                        page.NextContinuationToken,
                        observedContinuationTokens);
                    if (page.NextContinuationToken is not null)
                    {
                        observedContinuationTokens.Add(page.NextContinuationToken);
                    }

                    warnings.AddRange(page.Warnings);
                    containerConsistencyToken = page.ConsistencyToken ?? containerConsistencyToken;
                    if (containerKey == context.Request.ContainerKey)
                    {
                        rootConsistencyToken = containerConsistencyToken;
                    }

                    foreach (var item in page.Items)
                    {
                        if (!visitedItems.Add(item.Key))
                        {
                            continue;
                        }

                        scannedItems++;
                        if (context.Request.Filter.Matches(item)
                            && FileBrowserSearchMatching.MatchesText(item, context.Request.Query))
                        {
                            long itemBytes = FileBrowserSearchRetentionMeasure.Measure(item);
                            if (itemBytes > context.Request.Budget.MaximumRetainedBytes - retainedBytes)
                            {
                                budgetReached = true;
                                break;
                            }

                            matches.Add(item);
                            retainedBytes += itemBytes;
                            if (matches.Count >= context.Request.Budget.MaximumMatches)
                            {
                                budgetReached = true;
                                break;
                            }
                        }

                        if (item.IsContainer)
                        {
                            queue.Enqueue(item.Key);
                        }

                        if (scannedItems >= context.Request.Budget.MaximumItems)
                        {
                            budgetReached = true;
                            break;
                        }
                    }

                    continuationToken = budgetReached ? null : page.NextContinuationToken;
                }
                while (continuationToken is not null);
            }
        }
        catch (OperationCanceledException) when (
            durationCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            budgetReached = true;
        }

        if (budgetReached)
        {
            warnings.Add(new FileBrowserPageWarning(
                "search-budget-reached",
                $"Search stopped after {scannedContainers} containers or {scannedItems} items. Results are partial."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        TimeSpan elapsed = timeProvider.GetElapsedTime(started);
        return new ProgressiveSearchSnapshot(
            FileBrowserItemOrdering.Apply(matches, context.Request.Sort).ToArray(),
            budgetReached,
            scannedContainers,
            scannedItems,
            retainedBytes,
            browseRequests == 0 ? 0 : 1,
            elapsed,
            rootConsistencyToken,
            warnings.ToArray());
    }
}

