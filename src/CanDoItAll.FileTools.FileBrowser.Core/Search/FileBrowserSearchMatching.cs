namespace CanDoItAll.FileTools.FileBrowser;

internal static class FileBrowserSearchMatching
{
    public static IReadOnlyList<FileBrowserItem> FilterAndOrder(
        IEnumerable<FileBrowserItem> candidates,
        FileBrowserSearchRequest request)
        => FileBrowserItemOrdering.Apply(
            candidates
                .Where(request.Filter.Matches)
                .Where(item => MatchesText(item, request.Query)),
            request.Sort);

    public static FileBrowserSearchPage Page(
        IReadOnlyList<FileBrowserItem> matches,
        FileBrowserSearchRequest request,
        string strategyId,
        bool isPartial = false,
        int scannedContainers = 0,
        int scannedItems = 0,
        IReadOnlyList<FileBrowserPageWarning>? warnings = null)
    {
        var offset = ParseOffset(request.ContinuationToken);
        var page = matches.Skip(offset).Take(request.PageSize).ToArray();
        var nextOffset = offset + page.Length;
        return new FileBrowserSearchPage(
            page,
            strategyId,
            nextOffset < matches.Count ? $"offset:{nextOffset}" : null,
            matches.Count,
            isPartial,
            scannedContainers,
            scannedItems,
            request.ConsistencyToken,
            warnings,
            retainedItems: page.Length,
            retainedBytes: FileBrowserSearchRetentionMeasure.Measure(page));
    }

    public static bool MatchesText(FileBrowserItem item, string query)
        => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.DisplayPath?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
            || item.ContentIdentity?.Value.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private static int ParseOffset(string? continuationToken)
    {
        if (continuationToken is null)
        {
            return 0;
        }

        if (!continuationToken.StartsWith("offset:", StringComparison.Ordinal)
            || !int.TryParse(continuationToken.AsSpan("offset:".Length), out var offset)
            || offset < 0)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.StaleCursor,
                "The loaded-search continuation token is invalid.",
                isRetryable: true));
        }

        return offset;
    }
}

