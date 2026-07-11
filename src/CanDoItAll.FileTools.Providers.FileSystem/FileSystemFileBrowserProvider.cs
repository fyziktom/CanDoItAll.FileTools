using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.FileTools.Providers.FileSystem;

/// <summary>
/// Exposes one local directory as an always-current, root-confined, shallow file-browser source.
/// The provider describes host invocation eligibility but performs no host/UI action.
/// </summary>
public sealed class FileSystemFileBrowserProvider :
    IFileBrowserProvider,
    IFileBrowserContentProvider,
    IFileContentSource
{
    private static readonly IReadOnlySet<FileBrowserSortField> SupportedSortFields
        = new HashSet<FileBrowserSortField>
        {
            FileBrowserSortField.Name,
            FileBrowserSortField.ModifiedAt,
            FileBrowserSortField.Size,
            FileBrowserSortField.Type,
            FileBrowserSortField.Path
        };

    private readonly FileSystemPathResolver pathResolver;
    private readonly FileSystemItemFactory itemFactory;
    private readonly FileSystemContentReader contentReader;

    /// <summary>Creates a provider for one validated local filesystem root.</summary>
    public FileSystemFileBrowserProvider(FileSystemFileBrowserOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        pathResolver = new FileSystemPathResolver(options);
        itemFactory = new FileSystemItemFactory(options, pathResolver);
        contentReader = new FileSystemContentReader(pathResolver, itemFactory);
        Descriptor = new FileBrowserSourceDescriptor(
            options.SourceId,
            options.DisplayName,
            icon: "folder",
            description: "Root-confined local filesystem source.",
            capabilities: FileBrowserSourceCapabilities.PagedBrowse
                          | FileBrowserSourceCapabilities.ContentRead
                          | FileBrowserSourceCapabilities.RangeRead,
            recommendedPageSize: options.RecommendedPageSize,
            maximumPageSize: options.MaximumPageSize,
            supportedSortFields: SupportedSortFields,
            supportedSearchScopes:
            [
                FileBrowserSearchScope.LoadedFolder,
                FileBrowserSearchScope.LoadedDescendants,
                FileBrowserSearchScope.Progressive
            ],
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "filesystem",
                ["path-scope"] = "root-relative",
                ["hidden-items"] = options.IncludeHidden ? "included" : "excluded",
                ["reparse-points"] = options.ReparsePointPolicy == FileSystemReparsePointPolicy.ExposeAsLink
                    ? "inert-links"
                    : "excluded",
                ["freshness"] = "always-current",
                ["cache"] = "none"
            });
    }

    /// <summary>Gets the validated provider options.</summary>
    public FileSystemFileBrowserOptions Options { get; }

    /// <inheritdoc />
    public FileBrowserSourceDescriptor Descriptor { get; }

    /// <inheritdoc />
    public ValueTask<FileBrowserItem> GetRootAsync(
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
        => new(FileSystemProviderErrors.Execute(
            () =>
            {
                ArgumentNullException.ThrowIfNull(metadata);
                var root = pathResolver.ResolveRoot();
                return itemFactory.CreateRoot(itemFactory.Capture(root), metadata);
            },
            cancellationToken));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
        FileBrowserItemKey itemKey,
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
        => new(FileSystemProviderErrors.Execute<IReadOnlyList<FileBrowserItem>>(
            () =>
            {
                ArgumentNullException.ThrowIfNull(metadata);
                var resolvedPath = pathResolver.ResolvePath(itemKey);
                if (!resolvedPath[^1].IsDirectory)
                {
                    throw FileSystemProviderErrors.Create(
                        resolvedPath[^1].IsReparsePoint
                            ? FileBrowserErrorCode.Unsupported
                            : FileBrowserErrorCode.InvalidOperation,
                        resolvedPath[^1].IsReparsePoint
                            ? "Browsing through filesystem reparse points is disabled for this source."
                            : "Only directory occurrences can be resolved as a browser path.");
                }

                var items = new List<FileBrowserItem>(resolvedPath.Count);
                FileBrowserItemKey? parentKey = null;
                for (var index = 0; index < resolvedPath.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = resolvedPath[index];
                    var item = index == 0
                        ? itemFactory.CreateRoot(itemFactory.Capture(entry), metadata)
                        : itemFactory.Create(itemFactory.Capture(entry), parentKey, metadata);
                    items.Add(item);
                    parentKey = item.Key;
                }

                return items;
            },
            cancellationToken));

    /// <inheritdoc />
    public ValueTask<FileBrowserPage> BrowseAsync(
        FileBrowserBrowseRequest request,
        CancellationToken cancellationToken = default)
        => new(FileSystemProviderErrors.Execute(
            () => BrowseCore(request, cancellationToken),
            cancellationToken));

    /// <inheritdoc />
    public ValueTask<FileBrowserContentLease> OpenReadAsync(
        FileBrowserReadRequest request,
        CancellationToken cancellationToken = default)
        => new(FileSystemProviderErrors.Execute(
            () =>
            {
                ArgumentNullException.ThrowIfNull(request);
                var opened = contentReader.Open(
                    request.ItemKey,
                    request.Offset,
                    request.Length,
                    cancellationToken);
                return new FileBrowserContentLease(
                    opened.Stream,
                    opened.MediaType,
                    opened.Length,
                    ownsStream: true);
            },
            cancellationToken));

    /// <summary>
    /// Opens a host-issued file reference independently of any browser session. The reference value
    /// is the provider's canonical root-relative occurrence key and its source identifier must equal
    /// <see cref="FileSystemFileBrowserOptions.SourceId"/>. The returned revision is intentionally
    /// <see langword="null"/>: a mutable, shared local file handle does not provide a portable
    /// immutable snapshot identity suitable for optimistic concurrency.
    /// </summary>
    public ValueTask<FileContentLease> OpenReadAsync(
        FileContentReadRequest request,
        CancellationToken cancellationToken = default)
        => new(FileSystemProviderErrors.Execute(
            () =>
            {
                ArgumentNullException.ThrowIfNull(request);
                var opened = contentReader.Open(
                    request.File,
                    request.Offset,
                    request.Length,
                    cancellationToken);
                return new FileContentLease(
                    opened.Stream,
                    opened.MediaType,
                    opened.Length,
                    revision: null,
                    ownsStream: true);
            },
            cancellationToken));

    private FileBrowserPage BrowseCore(
        FileBrowserBrowseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        pathResolver.ValidateSource(request.ParentKey);
        if (request.IncludeDescendants)
        {
            throw FileSystemProviderErrors.Create(
                FileBrowserErrorCode.Unsupported,
                "The local filesystem provider supports shallow direct-child browsing only.");
        }

        if (request.PageSize > Options.MaximumPageSize)
        {
            throw FileSystemProviderErrors.Create(
                FileBrowserErrorCode.InvalidOperation,
                $"The requested page size exceeds this source's maximum of {Options.MaximumPageSize.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (!SupportedSortFields.Contains(request.Sort.Field))
        {
            throw FileSystemProviderErrors.Create(
                FileBrowserErrorCode.Unsupported,
                $"The local filesystem provider cannot sort by {request.Sort.Field}.");
        }

        var path = pathResolver.ResolvePath(request.ParentKey);
        var parent = path[^1];
        if (!parent.IsDirectory)
        {
            throw FileSystemProviderErrors.Create(
                parent.IsReparsePoint
                    ? FileBrowserErrorCode.Unsupported
                    : FileBrowserErrorCode.InvalidOperation,
                parent.IsReparsePoint
                    ? "Browsing through filesystem reparse points is disabled for this source."
                    : "The requested filesystem item is not a directory.");
        }

        var effectiveMetadata = CreateEffectiveMetadataRequest(request);
        var enumeration = EnumerateDirectChildren(parent, cancellationToken);
        var consistencyToken = CreateConsistencyToken(parent, enumeration.Entries);
        ValidateRequestedConsistency(request.ConsistencyToken, consistencyToken);

        var parentKey = pathResolver.CreateKey(parent.RelativeKey);
        var items = enumeration.Entries
            .Select(snapshot => itemFactory.Create(snapshot, parentKey, effectiveMetadata))
            .Where(request.Filter.Matches)
            .Order(new FileSystemFileBrowserItemComparer(request.Sort))
            .ToArray();

        var offset = request.ContinuationToken is null
            ? 0
            : FileSystemContinuationTokenCodec.DecodeAndValidate(
                request.ContinuationToken,
                request,
                consistencyToken);
        if (offset > items.Length)
        {
            throw FileSystemProviderErrors.Create(
                FileBrowserErrorCode.StaleCursor,
                "The filesystem continuation offset no longer exists for this browse query.",
                isRetryable: true);
        }

        var pageItems = items.Skip(offset).Take(request.PageSize).ToArray();
        var nextOffset = offset + pageItems.Length;
        var nextToken = nextOffset < items.Length
            ? FileSystemContinuationTokenCodec.Encode(request, consistencyToken, nextOffset)
            : null;

        return new FileBrowserPage(
            pageItems,
            nextToken,
            totalCount: items.Length,
            consistencyToken,
            enumeration.Warnings.Count == 0
                ? FileBrowserCompleteness.Complete
                : FileBrowserCompleteness.Partial,
            enumeration.Warnings);
    }

    private FileSystemEnumerationResult EnumerateDirectChildren(
        ResolvedFileSystemEntry parent,
        CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(parent.LogicalPath);
        directory.Refresh();
        if (!directory.Exists)
        {
            throw FileSystemProviderErrors.Create(
                FileBrowserErrorCode.NotFound,
                "The requested filesystem directory no longer exists.");
        }

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            IgnoreInaccessible = false,
            AttributesToSkip = 0
        };
        var children = new List<FileSystemEntrySnapshot>();
        var warnings = new List<FileBrowserPageWarning>();
        foreach (var info in directory.EnumerateFileSystemInfos("*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entry = pathResolver.ResolveChild(info);
                if (!pathResolver.IsVisible(entry))
                {
                    continue;
                }

                children.Add(itemFactory.Capture(entry));
            }
            catch (FileBrowserProviderException exception) when (exception.Error.Code is
                       FileBrowserErrorCode.NotFound
                       or FileBrowserErrorCode.Forbidden
                       or FileBrowserErrorCode.Unavailable)
            {
                warnings.Add(new FileBrowserPageWarning(
                    "filesystem-entry-skipped",
                    "A filesystem entry changed or could not be accessed while the folder was enumerated."));
            }
        }

        return new FileSystemEnumerationResult(children, warnings);
    }

    private static FileBrowserMetadataRequest CreateEffectiveMetadataRequest(
        FileBrowserBrowseRequest request)
    {
        var fields = request.Metadata.Fields
            | FileBrowserMetadataFields.Name
            | FileBrowserMetadataFields.Kind;
        fields |= request.Sort.Field switch
        {
            FileBrowserSortField.ModifiedAt => FileBrowserMetadataFields.Timestamps,
            FileBrowserSortField.Size => FileBrowserMetadataFields.Size,
            FileBrowserSortField.Type => FileBrowserMetadataFields.MediaType,
            FileBrowserSortField.Path => FileBrowserMetadataFields.DisplayPath,
            _ => FileBrowserMetadataFields.None
        };

        if (request.Filter.MediaTypePrefix is not null)
        {
            fields |= FileBrowserMetadataFields.MediaType;
        }

        return new FileBrowserMetadataRequest(fields, request.Metadata.IncludeExpensive);
    }

    private static string CreateConsistencyToken(
        ResolvedFileSystemEntry parent,
        IReadOnlyList<FileSystemEntrySnapshot> entries)
    {
        var directory = new DirectoryInfo(parent.LogicalPath);
        directory.Refresh();
        if (!directory.Exists)
        {
            throw FileSystemProviderErrors.Create(
                FileBrowserErrorCode.NotFound,
                "The requested filesystem directory no longer exists.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendHashValue(hash, "filesystem-v2");
        AppendHashValue(hash, parent.RelativeKey);
        AppendHashValue(hash, directory.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
        foreach (var snapshot in entries.OrderBy(entry => entry.Entry.RelativeKey, StringComparer.Ordinal))
        {
            AppendHashValue(hash, snapshot.Entry.RelativeKey);
            AppendHashValue(hash, ((int)snapshot.Entry.Attributes).ToString(CultureInfo.InvariantCulture));
            AppendHashValue(hash, snapshot.Entry.IsReparsePoint ? "link" : snapshot.Entry.IsDirectory ? "directory" : "file");
            AppendHashValue(hash, snapshot.Size?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendHashValue(hash, snapshot.CreatedAt?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendHashValue(hash, snapshot.ModifiedAt?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return $"fs-v2:{Convert.ToHexString(hash.GetHashAndReset())}";
    }

    private static void AppendHashValue(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(bytes);
        hash.AppendData([0]);
    }

    private static void ValidateRequestedConsistency(string? requested, string current)
    {
        if (requested is null || string.Equals(requested, current, StringComparison.Ordinal))
        {
            return;
        }

        throw FileSystemProviderErrors.Create(
            FileBrowserErrorCode.StaleCursor,
            "The filesystem directory changed after the previous page was loaded.",
            isRetryable: true);
    }

    private sealed record FileSystemEnumerationResult(
        IReadOnlyList<FileSystemEntrySnapshot> Entries,
        IReadOnlyList<FileBrowserPageWarning> Warnings);
}
