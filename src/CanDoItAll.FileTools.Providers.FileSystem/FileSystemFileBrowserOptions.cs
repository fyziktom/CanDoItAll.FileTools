using System.Security;
using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Providers.FileSystem;

/// <summary>Controls whether hidden filesystem entries are exposed by a configured source.</summary>
public enum FileSystemHiddenItemPolicy
{
    /// <summary>Exclude entries marked hidden and dot-prefixed entries.</summary>
    Exclude,

    /// <summary>Include hidden entries.</summary>
    Include
}

/// <summary>Controls how child reparse points are projected without ever following them.</summary>
public enum FileSystemReparsePointPolicy
{
    /// <summary>Expose a reparse point as an inert, non-navigable link item.</summary>
    ExposeAsLink,

    /// <summary>Exclude reparse points from enumeration and direct lookup.</summary>
    Exclude
}

/// <summary>Validated configuration for one root-confined local filesystem source.</summary>
public sealed record FileSystemFileBrowserOptions
{
    /// <summary>Creates validated options for one absolute, existing, non-reparse directory root.</summary>
    public FileSystemFileBrowserOptions(
        FileBrowserSourceId sourceId,
        string rootPath,
        string? displayName = null,
        bool includeHidden = false,
        FileSystemReparsePointPolicy reparsePointPolicy = FileSystemReparsePointPolicy.ExposeAsLink,
        int recommendedPageSize = 50,
        int maximumPageSize = 250)
    {
        if (string.IsNullOrWhiteSpace(sourceId.Value))
        {
            throw new ArgumentException("A source identifier is required.", nameof(sourceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Path.IsPathFullyQualified(rootPath))
        {
            throw new ArgumentException("The filesystem browser root must be an absolute path.", nameof(rootPath));
        }

        if (!Enum.IsDefined(reparsePointPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(reparsePointPolicy));
        }

        if (recommendedPageSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(recommendedPageSize));
        }

        if (maximumPageSize is < 1 or > 1000 || maximumPageSize < recommendedPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPageSize));
        }

        var normalizedRoot = NormalizeExistingRoot(rootPath);
        var fallbackDisplayName = GetSafeDefaultDisplayName(normalizedRoot);

        SourceId = sourceId;
        RootPath = normalizedRoot;
        var requestedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? fallbackDisplayName
            : displayName.Trim();
        DisplayName = ContainsAuthorizationRoot(requestedDisplayName, normalizedRoot)
            ? fallbackDisplayName
            : requestedDisplayName;
        HiddenItemPolicy = includeHidden
            ? FileSystemHiddenItemPolicy.Include
            : FileSystemHiddenItemPolicy.Exclude;
        ReparsePointPolicy = reparsePointPolicy;
        RecommendedPageSize = recommendedPageSize;
        MaximumPageSize = maximumPageSize;
    }

    /// <summary>Gets the configured source identifier.</summary>
    public FileBrowserSourceId SourceId { get; }

    /// <summary>
    /// Gets the normalized absolute authorization root. Providers must not project this value into
    /// descriptors, items, metadata, continuation tokens, or renderer-safe errors.
    /// </summary>
    public string RootPath { get; }

    /// <summary>Gets the source display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the hidden-entry policy.</summary>
    public FileSystemHiddenItemPolicy HiddenItemPolicy { get; }

    /// <summary>Gets whether hidden entries are included.</summary>
    public bool IncludeHidden => HiddenItemPolicy == FileSystemHiddenItemPolicy.Include;

    /// <summary>Gets the inert/excluded child reparse-point policy.</summary>
    public FileSystemReparsePointPolicy ReparsePointPolicy { get; }

    /// <summary>Gets the recommended browse page size.</summary>
    public int RecommendedPageSize { get; }

    /// <summary>Gets the maximum browse page size.</summary>
    public int MaximumPageSize { get; }

    private static string NormalizeExistingRoot(string rootPath)
    {
        var fullPath = Path.GetFullPath(rootPath);
        var rootPathValue = Path.GetPathRoot(fullPath);
        var normalized = string.Equals(fullPath, rootPathValue, PathComparison)
            ? fullPath
            : Path.TrimEndingDirectorySeparator(fullPath);

        try
        {
            var attributes = File.GetAttributes(normalized);
            if (!attributes.HasFlag(FileAttributes.Directory))
            {
                throw new ArgumentException("The filesystem browser root must be a directory.", nameof(rootPath));
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new ArgumentException("The filesystem browser root cannot be a reparse point.", nameof(rootPath));
            }

            return new DirectoryInfo(normalized).FullName;
        }
        catch (FileNotFoundException)
        {
            throw MissingRoot();
        }
        catch (DirectoryNotFoundException)
        {
            throw MissingRoot();
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                "Access to the filesystem browser root was denied.");
        }
        catch (SecurityException)
        {
            throw new UnauthorizedAccessException(
                "Access to the filesystem browser root was denied.");
        }
        catch (PathTooLongException)
        {
            throw new PathTooLongException(
                "The filesystem browser root path is too long.");
        }
        catch (IOException)
        {
            throw new IOException(
                "The filesystem browser root could not be inspected.");
        }
    }

    private static string GetSafeDefaultDisplayName(string normalizedRoot)
    {
        var name = new DirectoryInfo(normalizedRoot).Name;
        return string.IsNullOrWhiteSpace(name)
            || Path.IsPathRooted(name)
            || name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
                ? "Files"
                : name;
    }

    private static bool ContainsAuthorizationRoot(string displayName, string normalizedRoot)
    {
        var normalizedDisplayName = displayName
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var comparableRoot = normalizedRoot
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return normalizedDisplayName.Contains(comparableRoot, PathComparison);
    }

    private static DirectoryNotFoundException MissingRoot()
        => new("The filesystem browser root does not exist or is not accessible.");

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
