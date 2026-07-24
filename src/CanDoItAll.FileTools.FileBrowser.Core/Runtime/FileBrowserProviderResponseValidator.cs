namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Validates untrusted provider pages before they enter retained or renderer-visible state.</summary>
internal static class FileBrowserProviderResponseValidator
{
    public static void ValidateBrowsePage(FileBrowserBrowseRequest request, FileBrowserPage page)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(page);

        ValidatePageBounds(request.PageSize, page.Items.Count, page.TotalCount);
        ValidateCursorProgress(request.ContinuationToken, page.NextContinuationToken);
        var itemsByKey = ValidateItems(
            page.Items,
            request.ParentKey.SourceId,
            rejectEquivalentDuplicates: false);

        foreach (var item in itemsByKey.Values)
        {
            if (item.ParentKey == item.Key)
            {
                throw CorruptProviderResponse("The provider returned an item as its own parent.");
            }

            if (!request.IncludeDescendants && item.ParentKey != request.ParentKey)
            {
                throw CorruptProviderResponse(
                    "The provider returned an item outside the requested shallow folder.");
            }
        }

        ValidateVisibleParentChains(request, itemsByKey);
        ValidateConsistency(request.ConsistencyToken, page.ConsistencyToken);
    }

    public static void ValidateSearchPage(FileBrowserSearchRequest request, FileBrowserSearchPage page)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(page);

        ValidatePageBounds(request.PageSize, page.Items.Count, page.TotalCount);
        ValidateCursorProgress(request.ContinuationToken, page.NextContinuationToken);
        ValidateItems(
            page.Items,
            request.ContainerKey.SourceId,
            rejectEquivalentDuplicates: true);
        if (request.ContinuationToken is not null)
        {
            ValidateConsistency(request.ConsistencyToken, page.ConsistencyToken);
        }
    }

    public static void ValidateNoConflictingOverlaps(
        IEnumerable<FileBrowserItem> existingItems,
        IEnumerable<FileBrowserItem> incomingItems)
    {
        ArgumentNullException.ThrowIfNull(existingItems);
        ArgumentNullException.ThrowIfNull(incomingItems);

        var existingByKey = existingItems
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.Last());
        foreach (var incoming in incomingItems)
        {
            if (existingByKey.TryGetValue(incoming.Key, out var existing)
                && !HaveEquivalentDescriptors(existing, incoming))
            {
                throw CorruptProviderResponse(
                    "The provider returned conflicting descriptors for the same item across pages.");
            }
        }
    }

    public static void ValidateCursorNotPreviouslyObserved(
        string? nextContinuationToken,
        IReadOnlySet<string> observedContinuationTokens)
    {
        ArgumentNullException.ThrowIfNull(observedContinuationTokens);
        if (nextContinuationToken is not null
            && observedContinuationTokens.Contains(nextContinuationToken))
        {
            throw CorruptProviderResponse(
                "The provider returned a repeated continuation token that cannot advance the result.");
        }
    }

    public static FileBrowserCompleteness MergeCompleteness(
        FileBrowserCompleteness existing,
        FileBrowserCompleteness incoming)
        => (FileBrowserCompleteness)Math.Min((int)existing, (int)incoming);

    public static IReadOnlyList<FileBrowserPageWarning> MergeWarnings(
        IEnumerable<FileBrowserPageWarning> existing,
        IEnumerable<FileBrowserPageWarning> incoming)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);
        return existing.Concat(incoming).Distinct().ToArray();
    }

    public static bool HaveEquivalentDescriptors(FileBrowserItem left, FileBrowserItem right)
        => left.Key == right.Key
            && left.ParentKey == right.ParentKey
            && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && string.Equals(left.DisplayPath, right.DisplayPath, StringComparison.Ordinal)
            && left.Kind == right.Kind
            && left.Category == right.Category
            && left.ChildState == right.ChildState
            && left.ChildCount == right.ChildCount
            && left.Size == right.Size
            && string.Equals(left.MediaType, right.MediaType, StringComparison.Ordinal)
            && string.Equals(left.Owner, right.Owner, StringComparison.Ordinal)
            && left.CreatedAt == right.CreatedAt
            && left.ModifiedAt == right.ModifiedAt
            && left.ContentIdentity == right.ContentIdentity
            && left.MetadataState == right.MetadataState
            && left.Capabilities == right.Capabilities
            && string.Equals(left.OpenUri, right.OpenUri, StringComparison.Ordinal)
            && string.Equals(left.DownloadUri, right.DownloadUri, StringComparison.Ordinal)
            && HaveEquivalentMetadata(left.Metadata, right.Metadata);

    public static FileBrowserProviderException CorruptProviderResponse(string message)
        => new(new FileBrowserError(FileBrowserErrorCode.CorruptProviderResponse, message));

    private static Dictionary<FileBrowserItemKey, FileBrowserItem> ValidateItems(
        IEnumerable<FileBrowserItem> items,
        FileBrowserSourceId expectedSource,
        bool rejectEquivalentDuplicates)
    {
        var itemsByKey = new Dictionary<FileBrowserItemKey, FileBrowserItem>();
        foreach (var item in items)
        {
            if (item.Key.SourceId != expectedSource)
            {
                throw CorruptProviderResponse("The provider returned an item from another source.");
            }

            if (itemsByKey.TryGetValue(item.Key, out var existing))
            {
                if (!HaveEquivalentDescriptors(existing, item))
                {
                    throw CorruptProviderResponse(
                        "The provider returned conflicting descriptors for the same item.");
                }

                if (rejectEquivalentDuplicates)
                {
                    throw CorruptProviderResponse(
                        "The provider returned the same item occurrence more than once in one page.");
                }

                continue;
            }

            itemsByKey.Add(item.Key, item);
        }

        return itemsByKey;
    }

    private static void ValidatePageBounds(int requestedPageSize, int itemCount, long? totalCount)
    {
        if (itemCount > requestedPageSize)
        {
            throw CorruptProviderResponse(
                "The provider returned more items than the requested page size.");
        }

        if (totalCount.HasValue && totalCount.Value < itemCount)
        {
            throw CorruptProviderResponse(
                "The provider returned a total count smaller than the returned page.");
        }
    }

    private static void ValidateCursorProgress(
        string? consumedContinuationToken,
        string? nextContinuationToken)
    {
        if (consumedContinuationToken is not null
            && string.Equals(consumedContinuationToken, nextContinuationToken, StringComparison.Ordinal))
        {
            throw CorruptProviderResponse(
                "The provider returned the continuation token that was just consumed.");
        }
    }

    private static void ValidateConsistency(string? requestedToken, string? returnedToken)
    {
        if (requestedToken is not null
            && returnedToken is not null
            && !string.Equals(requestedToken, returnedToken, StringComparison.Ordinal))
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.StaleCursor,
                "The source revision changed while loading this result.",
                isRetryable: true));
        }
    }

    private static void ValidateVisibleParentChains(
        FileBrowserBrowseRequest request,
        IReadOnlyDictionary<FileBrowserItemKey, FileBrowserItem> itemsByKey)
    {
        foreach (var item in itemsByKey.Values)
        {
            var visited = new HashSet<FileBrowserItemKey> { item.Key };
            var parentKey = item.ParentKey;

            while (parentKey.HasValue
                   && itemsByKey.TryGetValue(parentKey.Value, out var visibleParent))
            {
                if (!visited.Add(visibleParent.Key))
                {
                    throw CorruptProviderResponse(
                        "The provider returned a parent cycle in the requested page.");
                }

                parentKey = visibleParent.ParentKey;
            }

            if (request.IncludeDescendants)
            {
                ValidateRecursiveParentChain(request, item, itemsByKey);
            }
        }
    }

    private static void ValidateRecursiveParentChain(
        FileBrowserBrowseRequest request,
        FileBrowserItem item,
        IReadOnlyDictionary<FileBrowserItemKey, FileBrowserItem> itemsByKey)
    {
        var parentKey = item.ParentKey;
        while (parentKey.HasValue)
        {
            if (parentKey.Value == request.ParentKey)
            {
                return;
            }

            if (!itemsByKey.TryGetValue(parentKey.Value, out var parent))
            {
                // Recursive pages can start in the middle of a hierarchy. An absent parent is
                // therefore an omitted ancestor, not proof of corruption.
                return;
            }

            parentKey = parent.ParentKey;
        }

        throw CorruptProviderResponse(
            "The provider returned a fully described parent chain that does not reach the requested folder.");
    }

    private static bool HaveEquivalentMetadata(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
        => left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out var value)
                && string.Equals(pair.Value, value, StringComparison.Ordinal));
}

