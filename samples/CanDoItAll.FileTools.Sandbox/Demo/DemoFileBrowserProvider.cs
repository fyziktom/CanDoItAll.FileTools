using System.Globalization;
using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Sandbox.Demo;

internal sealed class DemoFileBrowserProvider : IFileBrowserProvider
{
    private const string RootValue = "root";
    private const string ConsistencyToken = "sandbox-demo-v1";
    private readonly IReadOnlyDictionary<string, DemoNode> nodes;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<DemoNode>> children;
    private readonly DemoBehavior behavior;
    private int failuresRemaining;

    private DemoFileBrowserProvider(
        FileBrowserSourceDescriptor descriptor,
        IEnumerable<DemoNode> nodes,
        DemoBehavior behavior)
    {
        Descriptor = descriptor;
        this.behavior = behavior;
        failuresRemaining = behavior == DemoBehavior.RetryableFailure ? 1 : 0;
        this.nodes = nodes.ToDictionary(node => node.Key, StringComparer.Ordinal);
        children = this.nodes.Values
            .Where(node => node.Key != RootValue)
            .GroupBy(node => node.ParentKey ?? RootValue, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DemoNode>)group.ToArray(),
                StringComparer.Ordinal);
    }

    public FileBrowserSourceDescriptor Descriptor { get; }

    public static DemoFileBrowserProvider CreateProjectSource()
        => Create(
            "project",
            "Project files",
            "Application workspace and generated artifacts.",
            CreateProjectNodes(),
            DemoBehavior.Healthy);

    public static DemoFileBrowserProvider CreateSharedSource()
        => Create(
            "shared",
            "Shared resources",
            "Curated reusable files from an immutable source.",
            CreateSharedNodes(),
            DemoBehavior.Healthy);

    public static DemoFileBrowserProvider CreateEmptySource()
        => Create(
            "empty",
            "Empty project",
            "A configured source with no child items.",
            [DemoNode.Root("Empty project")],
            DemoBehavior.Empty);

    public static DemoFileBrowserProvider CreateWarningSource()
        => Create(
            "warning",
            "Process artifacts",
            "A usable listing with a non-fatal provider warning.",
            CreateWarningNodes(),
            DemoBehavior.Warning);

    public static DemoFileBrowserProvider CreateRetryableSource()
        => Create(
            "retry",
            "Remote storage",
            "Fails the first browse request and succeeds after Retry.",
            CreateRetryNodes(),
            DemoBehavior.RetryableFailure);

    public ValueTask<FileBrowserItem> GetRootAsync(
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ToItem(nodes[RootValue]));
    }

    public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
        FileBrowserItemKey itemKey,
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSource(itemKey);
        if (!nodes.TryGetValue(itemKey.Value, out DemoNode? current) || !current.IsContainer)
        {
            throw NotFound("The requested demo folder does not exist.");
        }

        var path = new Stack<FileBrowserItem>();
        while (current is not null)
        {
            path.Push(ToItem(current));
            current = current.ParentKey is null
                ? null
                : nodes.GetValueOrDefault(current.ParentKey);
        }

        return ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>(path.ToArray());
    }

    public ValueTask<FileBrowserPage> BrowseAsync(
        FileBrowserBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSource(request.ParentKey);
        if (!nodes.TryGetValue(request.ParentKey.Value, out DemoNode? parent) || !parent.IsContainer)
        {
            throw NotFound("The requested demo folder does not exist.");
        }

        if (behavior == DemoBehavior.RetryableFailure
            && Interlocked.CompareExchange(ref failuresRemaining, 0, 1) == 1)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.Unavailable,
                "The remote source is temporarily unavailable.",
                isRetryable: true,
                correlationId: "sandbox-retry"));
        }

        IEnumerable<DemoNode> candidates = request.IncludeDescendants
            ? EnumerateDescendants(parent.Key)
            : children.GetValueOrDefault(parent.Key, []);
        FileBrowserItem[] matching = ApplySort(
                candidates.Select(ToItem).Where(request.Filter.Matches),
                request.Sort)
            .ToArray();

        int offset = ParseContinuation(request.ContinuationToken, matching.Length);
        FileBrowserItem[] pageItems = matching.Skip(offset).Take(request.PageSize).ToArray();
        int nextOffset = offset + pageItems.Length;
        string? next = nextOffset < matching.Length
            ? nextOffset.ToString(CultureInfo.InvariantCulture)
            : null;
        IReadOnlyList<FileBrowserPageWarning> warnings = behavior == DemoBehavior.Warning
            ? [new FileBrowserPageWarning("sandbox-entry-skipped", "One changing artifact could not be inspected and was skipped.")]
            : [];

        return ValueTask.FromResult(new FileBrowserPage(
            pageItems,
            next,
            matching.Length,
            ConsistencyToken,
            behavior == DemoBehavior.Warning
                ? FileBrowserCompleteness.Partial
                : FileBrowserCompleteness.Complete,
            warnings));
    }

    private IEnumerable<DemoNode> EnumerateDescendants(string parentKey)
    {
        var pending = new Queue<DemoNode>(children.GetValueOrDefault(parentKey, []));
        while (pending.TryDequeue(out DemoNode? node))
        {
            yield return node;
            if (node.IsContainer)
            {
                foreach (DemoNode child in children.GetValueOrDefault(node.Key, []))
                {
                    pending.Enqueue(child);
                }
            }
        }
    }

    private FileBrowserItem ToItem(DemoNode node)
    {
        var sourceId = Descriptor.Id;
        var key = new FileBrowserItemKey(sourceId, node.Key);
        FileBrowserItemKey? parentKey = node.ParentKey is null
            ? null
            : new FileBrowserItemKey(sourceId, node.ParentKey);
        FileBrowserItemCapabilities capabilities = node.IsContainer
            ? FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate
            : FileBrowserItemCapabilities.Select
              | FileBrowserItemCapabilities.Open
              | FileBrowserItemCapabilities.CopyPath
              | FileBrowserItemCapabilities.CopyContentIdentity;

        return new FileBrowserItem(
            key,
            parentKey,
            node.Name,
            node.IsContainer ? FileBrowserItemKind.Container : FileBrowserItemKind.File,
            node.Category,
            node.DisplayPath,
            node.IsContainer
                ? children.GetValueOrDefault(node.Key, []).Count > 0
                    ? FileBrowserChildState.HasChildren
                    : FileBrowserChildState.Empty
                : FileBrowserChildState.Unknown,
            node.IsContainer ? children.GetValueOrDefault(node.Key, []).Count : null,
            node.Size,
            node.MediaType,
            node.Owner,
            node.ModifiedAt,
            node.ModifiedAt,
            node.IsContainer ? null : new FileBrowserContentIdentity("demo", $"{Descriptor.Id.Value}-{node.Key}"),
            capabilities: capabilities);
    }

    private static IEnumerable<FileBrowserItem> ApplySort(
        IEnumerable<FileBrowserItem> items,
        FileBrowserSortDescriptor sort)
    {
        Func<FileBrowserItem, object?> selector = sort.Field switch
        {
            FileBrowserSortField.ModifiedAt => item => item.ModifiedAt,
            FileBrowserSortField.Size => item => item.Size,
            FileBrowserSortField.Type => item => item.MediaType ?? item.Category.ToString(),
            FileBrowserSortField.Owner => item => item.Owner,
            FileBrowserSortField.Path => item => item.DisplayPath,
            _ => item => item.Name
        };
        IComparer<object?> comparer = sort.Direction == FileBrowserSortDirection.Ascending
            ? Comparer<object?>.Create(CompareValues)
            : Comparer<object?>.Create((left, right) => CompareValues(right, left));
        IOrderedEnumerable<FileBrowserItem> ordered = sort.FoldersFirst
            ? items.OrderByDescending(item => item.IsContainer).ThenBy(selector, comparer)
            : items.OrderBy(selector, comparer);
        return ordered.ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static int CompareValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        return left switch
        {
            string leftString => StringComparer.OrdinalIgnoreCase.Compare(leftString, right.ToString()),
            IComparable comparable => comparable.CompareTo(right),
            _ => StringComparer.Ordinal.Compare(left.ToString(), right.ToString())
        };
    }

    private static int ParseContinuation(string? token, int count)
    {
        if (token is null)
        {
            return 0;
        }

        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int offset)
            || offset < 0
            || offset > count)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.StaleCursor,
                "The demo page cursor is no longer valid.",
                isRetryable: true));
        }

        return offset;
    }

    private void ValidateSource(FileBrowserItemKey key)
    {
        if (key.SourceId != Descriptor.Id)
        {
            throw NotFound("The requested item belongs to another source.");
        }
    }

    private static FileBrowserProviderException NotFound(string message)
        => new(new FileBrowserError(FileBrowserErrorCode.NotFound, message));

    private static DemoFileBrowserProvider Create(
        string sourceId,
        string displayName,
        string description,
        IEnumerable<DemoNode> nodes,
        DemoBehavior behavior)
    {
        var id = new FileBrowserSourceId(sourceId);
        var descriptor = new FileBrowserSourceDescriptor(
            id,
            displayName,
            icon: "folder",
            description,
            capabilities: FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.RecursiveBrowse,
            recommendedPageSize: 8,
            maximumPageSize: 64,
            supportedSearchScopes:
            [
                FileBrowserSearchScope.LoadedFolder,
                FileBrowserSearchScope.LoadedDescendants,
                FileBrowserSearchScope.Progressive
            ]);
        return new DemoFileBrowserProvider(descriptor, nodes, behavior);
    }

    private static IReadOnlyList<DemoNode> CreateProjectNodes()
    {
        var nodes = new List<DemoNode>
        {
            DemoNode.Root("Project files"),
            DemoNode.Folder("src", "root", "src", "src"),
            DemoNode.Folder("docs", "root", "docs", "docs"),
            DemoNode.Folder("artifacts", "root", "artifacts", "artifacts"),
            DemoNode.File("readme", "root", "README.md", "README.md", FileBrowserItemCategory.Document, 4_820, "text/markdown"),
            DemoNode.File("solution", "root", "CanDoItAll.slnx", "CanDoItAll.slnx", FileBrowserItemCategory.Code, 9_144, "application/xml"),
            DemoNode.File("roadmap", "root", "roadmap.mmd", "roadmap.mmd", FileBrowserItemCategory.Document, 2_190, "text/plain"),
            DemoNode.File("settings", "root", "settings.json", "settings.json", FileBrowserItemCategory.Data, 1_384, "application/json"),
            DemoNode.File("src-app", "src", "App.razor", "src/App.razor", FileBrowserItemCategory.Code, 3_286, "text/plain"),
            DemoNode.File("src-program", "src", "Program.cs", "src/Program.cs", FileBrowserItemCategory.Code, 1_108, "text/plain"),
            DemoNode.File("docs-architecture", "docs", "architecture.md", "docs/architecture.md", FileBrowserItemCategory.Document, 18_230, "text/markdown"),
            DemoNode.File("docs-contract", "docs", "host-contract.md", "docs/host-contract.md", FileBrowserItemCategory.Document, 7_412, "text/markdown"),
            DemoNode.File("artifact-report", "artifacts", "run-report.pdf", "artifacts/run-report.pdf", FileBrowserItemCategory.Document, 824_330, "application/pdf"),
            DemoNode.File("artifact-table", "artifacts", "metrics.csv", "artifacts/metrics.csv", FileBrowserItemCategory.Data, 31_875, "text/csv")
        };
        for (int index = 1; index <= 20; index++)
        {
            nodes.Add(DemoNode.File(
                $"note-{index:00}",
                "root",
                $"work-note-{index:00}.md",
                $"work-note-{index:00}.md",
                FileBrowserItemCategory.Document,
                1_100 + index * 137,
                "text/markdown"));
        }

        return nodes;
    }

    private static IReadOnlyList<DemoNode> CreateSharedNodes()
        =>
        [
            DemoNode.Root("Shared resources"),
            DemoNode.Folder("brand", "root", "brand", "brand"),
            DemoNode.Folder("templates", "root", "templates", "templates"),
            DemoNode.File("manual", "root", "team-manual.pdf", "team-manual.pdf", FileBrowserItemCategory.Document, 2_481_500, "application/pdf"),
            DemoNode.File("brand-cover", "brand", "cover.png", "brand/cover.png", FileBrowserItemCategory.Image, 482_920, "image/png"),
            DemoNode.File("brand-logo", "brand", "mark.svg", "brand/mark.svg", FileBrowserItemCategory.Image, 7_440, "image/svg+xml"),
            DemoNode.File("template-brief", "templates", "project-brief.md", "templates/project-brief.md", FileBrowserItemCategory.Document, 5_220, "text/markdown"),
            DemoNode.File("template-data", "templates", "import-template.xlsx", "templates/import-template.xlsx", FileBrowserItemCategory.Data, 62_800, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        ];

    private static IReadOnlyList<DemoNode> CreateWarningNodes()
        =>
        [
            DemoNode.Root("Process artifacts"),
            DemoNode.Folder("output", "root", "output", "output"),
            DemoNode.File("summary", "root", "summary.md", "summary.md", FileBrowserItemCategory.Document, 8_120, "text/markdown"),
            DemoNode.File("trace", "root", "trace.json", "trace.json", FileBrowserItemCategory.Data, 92_200, "application/json"),
            DemoNode.File("output-archive", "output", "release.zip", "output/release.zip", FileBrowserItemCategory.Archive, 4_920_800, "application/zip")
        ];

    private static IReadOnlyList<DemoNode> CreateRetryNodes()
        =>
        [
            DemoNode.Root("Remote storage"),
            DemoNode.Folder("incoming", "root", "incoming", "incoming"),
            DemoNode.File("remote-note", "root", "connection-note.txt", "connection-note.txt", FileBrowserItemCategory.Document, 640, "text/plain"),
            DemoNode.File("remote-image", "incoming", "field-photo.jpg", "incoming/field-photo.jpg", FileBrowserItemCategory.Image, 1_820_000, "image/jpeg")
        ];

    private enum DemoBehavior
    {
        Healthy,
        Empty,
        Warning,
        RetryableFailure
    }

    private sealed record DemoNode(
        string Key,
        string? ParentKey,
        string Name,
        string DisplayPath,
        bool IsContainer,
        FileBrowserItemCategory Category,
        long? Size,
        string? MediaType,
        string Owner,
        DateTimeOffset ModifiedAt)
    {
        public static DemoNode Root(string name)
            => new(RootValue, null, name, string.Empty, true, FileBrowserItemCategory.Folder, null, null, "system", DateTimeOffset.UtcNow.AddMinutes(-2));

        public static DemoNode Folder(string key, string parentKey, string name, string path)
            => new(key, parentKey, name, path, true, FileBrowserItemCategory.Folder, null, null, "team", DateTimeOffset.UtcNow.AddMinutes(-18));

        public static DemoNode File(
            string key,
            string parentKey,
            string name,
            string path,
            FileBrowserItemCategory category,
            long size,
            string mediaType)
            => new(key, parentKey, name, path, false, category, size, mediaType, "agent", DateTimeOffset.UtcNow.AddMinutes(-37));
    }
}
