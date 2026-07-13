using System.Collections.ObjectModel;

namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>A non-fatal provider warning attached to a page.</summary>
public sealed record FileBrowserPageWarning(string Code, string Message, FileBrowserItemKey? ItemKey = null)
{
    public string Code { get; } = string.IsNullOrWhiteSpace(Code) ? throw new ArgumentException("A warning code is required.", nameof(Code)) : Code.Trim();
    public string Message { get; } = string.IsNullOrWhiteSpace(Message) ? throw new ArgumentException("A warning message is required.", nameof(Message)) : Message.Trim();
}

/// <summary>A shallow provider page.</summary>
public sealed record FileBrowserPage
{
    public FileBrowserPage(
        IEnumerable<FileBrowserItem> items,
        string? nextContinuationToken = null,
        long? totalCount = null,
        string? consistencyToken = null,
        FileBrowserCompleteness completeness = FileBrowserCompleteness.Complete,
        IEnumerable<FileBrowserPageWarning>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount));
        }

        Items = Array.AsReadOnly(items.ToArray());
        NextContinuationToken = string.IsNullOrWhiteSpace(nextContinuationToken) ? null : nextContinuationToken;
        TotalCount = totalCount;
        ConsistencyToken = string.IsNullOrWhiteSpace(consistencyToken) ? null : consistencyToken;
        Completeness = completeness;
        Warnings = Array.AsReadOnly((warnings ?? []).ToArray());
    }

    public IReadOnlyList<FileBrowserItem> Items { get; }

    public string? NextContinuationToken { get; }

    public long? TotalCount { get; }

    public string? ConsistencyToken { get; }

    public FileBrowserCompleteness Completeness { get; }

    public IReadOnlyList<FileBrowserPageWarning> Warnings { get; }

    public bool HasMore => NextContinuationToken is not null;
}

/// <summary>A page returned by any search strategy.</summary>
public sealed record FileBrowserSearchPage
{
    public FileBrowserSearchPage(
        IEnumerable<FileBrowserItem> items,
        string strategyId,
        string? nextContinuationToken = null,
        long? totalCount = null,
        bool isPartial = false,
        int scannedContainers = 0,
        int scannedItems = 0,
        string? consistencyToken = null,
        IEnumerable<FileBrowserPageWarning>? warnings = null,
        int? retainedItems = null,
        long retainedBytes = 0,
        int peakConcurrentRequests = 0,
        TimeSpan elapsed = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyId);
        if (totalCount < 0 ||
            scannedContainers < 0 ||
            scannedItems < 0 ||
            retainedItems < 0 ||
            retainedBytes < 0 ||
            peakConcurrentRequests < 0 ||
            elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount));
        }

        Items = Array.AsReadOnly(items.ToArray());
        StrategyId = strategyId.Trim();
        NextContinuationToken = string.IsNullOrWhiteSpace(nextContinuationToken) ? null : nextContinuationToken;
        TotalCount = totalCount;
        IsPartial = isPartial;
        ScannedContainers = scannedContainers;
        ScannedItems = scannedItems;
        ConsistencyToken = string.IsNullOrWhiteSpace(consistencyToken) ? null : consistencyToken;
        Warnings = Array.AsReadOnly((warnings ?? []).ToArray());
        RetainedItems = retainedItems ?? Items.Count;
        RetainedBytes = retainedBytes;
        PeakConcurrentRequests = peakConcurrentRequests;
        Elapsed = elapsed;
    }

    public IReadOnlyList<FileBrowserItem> Items { get; }

    public string StrategyId { get; }

    public string? NextContinuationToken { get; }

    public long? TotalCount { get; }

    public bool IsPartial { get; }

    public int ScannedContainers { get; }

    public int ScannedItems { get; }

    public string? ConsistencyToken { get; }

    public IReadOnlyList<FileBrowserPageWarning> Warnings { get; }

    public int RetainedItems { get; }

    public long RetainedBytes { get; }

    public int PeakConcurrentRequests { get; }

    public TimeSpan Elapsed { get; }

    public bool HasMore => NextContinuationToken is not null;
}
