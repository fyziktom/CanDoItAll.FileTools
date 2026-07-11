using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Providers.FileSystem;

internal sealed record ResolvedFileSystemEntry(
    string LogicalPath,
    string RelativeKey,
    string Name,
    FileAttributes Attributes,
    bool IsReparsePoint,
    bool IsDirectory);

/// <summary>Owns occurrence-key normalization, root confinement, and the never-follow policy.</summary>
internal sealed class FileSystemPathResolver
{
    private readonly FileSystemFileBrowserOptions options;

    public FileSystemPathResolver(FileSystemFileBrowserOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public ResolvedFileSystemEntry ResolveRoot()
    {
        var root = ResolveExistingEntry(options.RootPath, ".", options.DisplayName);
        if (root.IsReparsePoint)
        {
            throw ProviderError(
                FileBrowserErrorCode.Forbidden,
                "The configured filesystem source root is no longer safe to access.");
        }

        if (!root.IsDirectory)
        {
            throw ProviderError(
                FileBrowserErrorCode.NotFound,
                "The configured filesystem source root is no longer available.");
        }

        return root;
    }

    public IReadOnlyList<ResolvedFileSystemEntry> ResolvePath(FileBrowserItemKey itemKey)
    {
        ValidateKey(itemKey);
        var root = ResolveRoot();
        if (itemKey.Value == ".")
        {
            return [root];
        }

        var segments = ParseCanonicalSegments(itemKey.Value);
        var results = new List<ResolvedFileSystemEntry>(segments.Length + 1) { root };
        var currentPath = options.RootPath;

        for (var index = 0; index < segments.Length; index++)
        {
            currentPath = Path.Combine(currentPath, segments[index]);
            EnsureContained(currentPath);

            var relativeKey = string.Join('/', segments.Take(index + 1));
            var entry = ResolveExistingEntry(currentPath, relativeKey, segments[index]);
            EnsureExposed(entry);
            if (!options.IncludeHidden && IsHidden(entry.Name, entry.Attributes))
            {
                throw ProviderError(
                    FileBrowserErrorCode.NotFound,
                    "The requested filesystem item is not available in this source.");
            }

            if (index < segments.Length - 1 && !entry.IsDirectory)
            {
                throw ProviderError(
                    entry.IsReparsePoint
                        ? FileBrowserErrorCode.Unsupported
                        : FileBrowserErrorCode.InvalidLocation,
                    entry.IsReparsePoint
                        ? "Browsing through filesystem reparse points is disabled for this source."
                        : "The requested filesystem path traverses a non-directory item.");
            }

            results.Add(entry);
        }

        return results;
    }

    public ResolvedFileSystemEntry ResolveChild(FileSystemInfo logicalInfo)
    {
        ArgumentNullException.ThrowIfNull(logicalInfo);
        var logicalPath = Path.GetFullPath(logicalInfo.FullName);
        EnsureContained(logicalPath);
        return ResolveExistingEntry(logicalPath, ToRelativeKey(logicalPath), logicalInfo.Name);
    }

    public ResolvedFileSystemEntry Refresh(ResolvedFileSystemEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var current = ResolveExistingEntry(entry.LogicalPath, entry.RelativeKey, entry.Name);
        EnsureExposed(current);
        return current;
    }

    public bool IsVisible(ResolvedFileSystemEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return (options.ReparsePointPolicy != FileSystemReparsePointPolicy.Exclude || !entry.IsReparsePoint)
            && (options.IncludeHidden || !IsHidden(entry.Name, entry.Attributes));
    }

    public FileBrowserItemKey CreateKey(string relativeKey)
        => new(options.SourceId, relativeKey);

    public string ToRelativeKey(string fullPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        EnsureContained(normalizedFullPath);
        var relativePath = Path.GetRelativePath(options.RootPath, normalizedFullPath);
        return relativePath == "."
            ? "."
            : relativePath
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    public void ValidateSource(FileBrowserItemKey itemKey)
        => ValidateKey(itemKey);

    private ResolvedFileSystemEntry ResolveExistingEntry(
        string logicalPath,
        string relativeKey,
        string name)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(logicalPath);
        }
        catch (FileNotFoundException)
        {
            throw ProviderError(
                FileBrowserErrorCode.NotFound,
                "The requested filesystem item no longer exists.");
        }
        catch (DirectoryNotFoundException)
        {
            throw ProviderError(
                FileBrowserErrorCode.NotFound,
                "The requested filesystem item no longer exists.");
        }

        var isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);
        return new ResolvedFileSystemEntry(
            logicalPath,
            relativeKey,
            name,
            attributes,
            isReparsePoint,
            IsDirectory: !isReparsePoint && attributes.HasFlag(FileAttributes.Directory));
    }

    private void EnsureExposed(ResolvedFileSystemEntry entry)
    {
        if (entry.IsReparsePoint
            && options.ReparsePointPolicy == FileSystemReparsePointPolicy.Exclude)
        {
            throw ProviderError(
                FileBrowserErrorCode.NotFound,
                "The requested filesystem item is not available in this source.");
        }
    }

    private void ValidateKey(FileBrowserItemKey itemKey)
    {
        if (itemKey.SourceId != options.SourceId)
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidLocation,
                "The requested filesystem item belongs to a different source.");
        }

        if (itemKey.Revision is not null)
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidLocation,
                "Local filesystem occurrence keys do not support revisions.");
        }

        if (itemKey.Value == ".")
        {
            return;
        }

        var segments = ParseCanonicalSegments(itemKey.Value);
        var candidate = Path.GetFullPath(Path.Combine([options.RootPath, .. segments]));
        EnsureContained(candidate);
        var canonicalKey = ToRelativeKey(candidate);
        if (!string.Equals(canonicalKey, itemKey.Value, StringComparison.Ordinal))
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidLocation,
                "The filesystem occurrence key is not in canonical root-relative form.");
        }
    }

    private static string[] ParseCanonicalSegments(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || Path.IsPathFullyQualified(value)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.EndsWith("/", StringComparison.Ordinal))
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidLocation,
                "The filesystem occurrence key must be a normalized root-relative path.");
        }

        var segments = value.Split('/');
        if (segments.Length == 0
            || segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidLocation,
                "The filesystem occurrence key contains an invalid or traversal segment.");
        }

        var invalidFileNameCharacters = Path.GetInvalidFileNameChars();
        foreach (var segment in segments)
        {
            if (segment.IndexOfAny(invalidFileNameCharacters) >= 0
                || Path.IsPathRooted(segment)
                || OperatingSystem.IsWindows() && segment.Contains('\\'))
            {
                throw ProviderError(
                    FileBrowserErrorCode.InvalidLocation,
                    "The filesystem occurrence key contains an invalid path segment.");
            }
        }

        return segments;
    }

    private void EnsureContained(string candidatePath)
    {
        var relativePath = Path.GetRelativePath(options.RootPath, Path.GetFullPath(candidatePath));
        var escapesRoot = Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        if (escapesRoot)
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidLocation,
                "The requested filesystem path escapes the configured source root.");
        }
    }

    private static bool IsHidden(string name, FileAttributes attributes)
        => attributes.HasFlag(FileAttributes.Hidden)
           || name.StartsWith(".", StringComparison.Ordinal);

    private static FileBrowserProviderException ProviderError(
        FileBrowserErrorCode code,
        string message)
        => FileSystemProviderErrors.Create(code, message);
}
