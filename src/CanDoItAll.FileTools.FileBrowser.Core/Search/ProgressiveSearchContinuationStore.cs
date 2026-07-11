using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.FileTools.FileBrowser;

internal sealed record ProgressiveSearchSnapshot(
    IReadOnlyList<FileBrowserItem> Matches,
    bool IsPartial,
    int ScannedContainers,
    int ScannedItems,
    string? ConsistencyToken,
    IReadOnlyList<FileBrowserPageWarning> Warnings);

internal sealed record ProgressiveSearchContinuation(
    IReadOnlyList<FileBrowserItem> Items,
    string? NextContinuationToken,
    long TotalCount,
    bool IsPartial,
    int ScannedContainers,
    int ScannedItems,
    string? ConsistencyToken,
    IReadOnlyList<FileBrowserPageWarning> Warnings);

/// <summary>
/// Stores stable progressive-search result snapshots behind random, idempotent page cursors.
/// Capacity and absolute expiry bound the state owned by a session-scoped strategy instance.
/// </summary>
internal sealed class ProgressiveSearchContinuationStore
{
    private const string TokenPrefix = "pfs1.";
    private readonly object sync = new();
    private readonly Dictionary<string, RetainedSearch> retained = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RetainedCursor> cursors = new(StringComparer.Ordinal);
    private readonly LinkedList<string> leastRecentlyUsed = [];
    private readonly int maximumRetainedSearches;
    private readonly TimeSpan retention;
    private readonly TimeProvider timeProvider;

    public ProgressiveSearchContinuationStore(
        int maximumRetainedSearches,
        TimeSpan retention,
        TimeProvider timeProvider)
    {
        if (maximumRetainedSearches < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRetainedSearches));
        }

        if (retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retention));
        }

        this.maximumRetainedSearches = maximumRetainedSearches;
        this.retention = retention;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public string Retain(
        ProgressiveSearchSnapshot snapshot,
        ProgressiveSearchRequestFingerprint fingerprint,
        int nextOffset)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (nextOffset < 1 || nextOffset >= snapshot.Matches.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(nextOffset));
        }

        lock (sync)
        {
            var now = timeProvider.GetUtcNow();
            PurgeExpired(now);
            while (retained.Count >= maximumRetainedSearches)
            {
                Remove(leastRecentlyUsed.First!.Value);
            }

            string token;
            do
            {
                token = CreateToken();
            }
            while (cursors.ContainsKey(token));

            LinkedListNode<string> recencyNode = leastRecentlyUsed.AddLast(token);
            var search = new RetainedSearch(
                snapshot,
                fingerprint,
                now + retention,
                recencyNode);
            search.Tokens.Add(token);
            retained.Add(token, search);
            cursors.Add(token, new RetainedCursor(search, nextOffset));
            return token;
        }
    }

    public ProgressiveSearchContinuation TakeNextPage(
        string token,
        ProgressiveSearchRequestFingerprint fingerprint,
        string? consistencyToken,
        int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PurgeExpired(timeProvider.GetUtcNow());
            if (!cursors.TryGetValue(token, out RetainedCursor? cursor))
            {
                throw CreateStaleCursorException();
            }

            RetainedSearch search = cursor.Search;
            if (search.Fingerprint != fingerprint
                || !string.Equals(
                    search.Snapshot.ConsistencyToken,
                    consistencyToken,
                    StringComparison.Ordinal))
            {
                throw CreateStaleCursorException();
            }

            var items = search.Snapshot.Matches
                .Skip(cursor.Offset)
                .Take(pageSize)
                .ToArray();
            if (items.Length == 0)
            {
                throw CreateStaleCursorException();
            }

            var nextOffset = cursor.Offset + items.Length;
            var addNextCursor = false;
            string? nextToken;
            if (nextOffset >= search.Snapshot.Matches.Count)
            {
                nextToken = null;
            }
            else
            {
                nextToken = cursor.NextToken;
                if (nextToken is null)
                {
                    do
                    {
                        nextToken = CreateToken();
                    }
                    while (cursors.ContainsKey(nextToken));
                    addNextCursor = true;
                }
            }

            var result = new ProgressiveSearchContinuation(
                items,
                nextToken,
                search.Snapshot.Matches.Count,
                search.Snapshot.IsPartial,
                search.Snapshot.ScannedContainers,
                search.Snapshot.ScannedItems,
                search.Snapshot.ConsistencyToken,
                search.Snapshot.Warnings);
            cancellationToken.ThrowIfCancellationRequested();
            if (addNextCursor)
            {
                cursor.NextToken = nextToken;
                search.Tokens.Add(nextToken!);
                cursors.Add(nextToken!, new RetainedCursor(search, nextOffset));
            }

            // The cursor remains immutable and reusable. If the session is canceled after this
            // method returns but before it publishes, retrying the same token returns this exact
            // page and next token instead of skipping data.
            leastRecentlyUsed.Remove(search.RecencyNode);
            leastRecentlyUsed.AddLast(search.RecencyNode);
            return result;
        }
    }

    private static string CreateToken()
        => TokenPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static FileBrowserProviderException CreateStaleCursorException()
        => new(new FileBrowserError(
            FileBrowserErrorCode.StaleCursor,
            "The progressive-search continuation token is missing, expired, or does not match this search.",
            isRetryable: true));

    private void PurgeExpired(DateTimeOffset now)
    {
        foreach (string token in retained
            .Where(pair => pair.Value.ExpiresAt <= now)
            .Select(pair => pair.Key)
            .ToArray())
        {
            Remove(token);
        }
    }

    private void Remove(string token)
    {
        if (retained.Remove(token, out RetainedSearch? search))
        {
            leastRecentlyUsed.Remove(search.RecencyNode);
            foreach (string cursorToken in search.Tokens)
            {
                cursors.Remove(cursorToken);
            }
        }
    }

    private sealed class RetainedSearch(
        ProgressiveSearchSnapshot snapshot,
        ProgressiveSearchRequestFingerprint fingerprint,
        DateTimeOffset expiresAt,
        LinkedListNode<string> recencyNode)
    {
        public ProgressiveSearchSnapshot Snapshot { get; } = snapshot;

        public ProgressiveSearchRequestFingerprint Fingerprint { get; } = fingerprint;

        public DateTimeOffset ExpiresAt { get; } = expiresAt;

        public LinkedListNode<string> RecencyNode { get; } = recencyNode;

        public HashSet<string> Tokens { get; } = new(StringComparer.Ordinal);
    }

    private sealed class RetainedCursor(RetainedSearch search, int offset)
    {
        public RetainedSearch Search { get; } = search;

        public int Offset { get; } = offset;

        public string? NextToken { get; set; }
    }
}

/// <summary>Canonical identity for every search input except continuation and consistency revision.</summary>
internal readonly record struct ProgressiveSearchRequestFingerprint(string Value)
{
    public static ProgressiveSearchRequestFingerprint Create(
        IFileBrowserProvider provider,
        FileBrowserSearchRequest request)
    {
        var canonical = new StringBuilder();
        Append(canonical, provider.Descriptor.Id.Value);
        Append(canonical, request.ContainerKey.SourceId.Value);
        Append(canonical, request.ContainerKey.Value);
        Append(canonical, request.ContainerKey.Revision);
        Append(canonical, request.Query);
        Append(canonical, ((int)request.Scope).ToString(CultureInfo.InvariantCulture));
        Append(canonical, request.PageSize.ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)request.Sort.Field).ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)request.Sort.Direction).ToString(CultureInfo.InvariantCulture));
        Append(canonical, request.Sort.FoldersFirst ? "1" : "0");
        Append(canonical, request.Filter.MediaTypePrefix);
        AppendSet(canonical, request.Filter.Kinds.Select(value => ((int)value).ToString(CultureInfo.InvariantCulture)));
        AppendSet(canonical, request.Filter.Categories.Select(value => ((int)value).ToString(CultureInfo.InvariantCulture)));
        AppendSet(canonical, request.Filter.Extensions, StringComparer.OrdinalIgnoreCase);
        Append(canonical, request.Budget.MaximumContainers.ToString(CultureInfo.InvariantCulture));
        Append(canonical, request.Budget.MaximumItems.ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)request.Metadata.Fields).ToString(CultureInfo.InvariantCulture));
        Append(canonical, request.Metadata.IncludeExpensive ? "1" : "0");

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return new ProgressiveSearchRequestFingerprint(Convert.ToHexString(hash));
    }

    private static void AppendSet(
        StringBuilder builder,
        IEnumerable<string> values,
        IComparer<string>? comparer = null)
    {
        string[] ordered = values.Order(comparer ?? StringComparer.Ordinal).ToArray();
        Append(builder, ordered.Length.ToString(CultureInfo.InvariantCulture));
        foreach (string value in ordered)
        {
            Append(builder, value);
        }
    }

    private static void Append(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
    }
}

