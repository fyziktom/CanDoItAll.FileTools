using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Providers.FileSystem;

/// <summary>Provider-local deterministic ordering keeps this adapter independently selectable.</summary>
internal sealed class FileSystemFileBrowserItemComparer : IComparer<FileBrowserItem>
{
    private readonly FileBrowserSortDescriptor sort;

    public FileSystemFileBrowserItemComparer(FileBrowserSortDescriptor sort)
    {
        this.sort = sort ?? throw new ArgumentNullException(nameof(sort));
    }

    public int Compare(FileBrowserItem? x, FileBrowserItem? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        if (sort.FoldersFirst && x.IsContainer != y.IsContainer)
        {
            return x.IsContainer ? -1 : 1;
        }

        var result = sort.Field switch
        {
            FileBrowserSortField.ModifiedAt => CompareNullable(x.ModifiedAt, y.ModifiedAt),
            FileBrowserSortField.Size => CompareNullable(x.Size, y.Size),
            FileBrowserSortField.Type => CompareText(x.MediaType ?? x.Category.ToString(), y.MediaType ?? y.Category.ToString()),
            FileBrowserSortField.Owner => CompareText(x.Owner, y.Owner),
            FileBrowserSortField.Path => CompareText(x.DisplayPath, y.DisplayPath),
            _ => CompareText(x.Name, y.Name)
        };

        if (result != 0 && sort.Direction == FileBrowserSortDirection.Descending)
        {
            result = -result;
        }

        if (result == 0)
        {
            result = CompareText(x.Name, y.Name);
        }

        return result == 0
            ? StringComparer.Ordinal.Compare(x.Key.ToString(), y.Key.ToString())
            : result;
    }

    private static int CompareText(string? left, string? right)
        => StringComparer.OrdinalIgnoreCase.Compare(left ?? string.Empty, right ?? string.Empty);

    private static int CompareNullable<T>(T? left, T? right)
        where T : struct, IComparable<T>
    {
        if (!left.HasValue)
        {
            return right.HasValue ? 1 : 0;
        }

        return !right.HasValue ? -1 : left.Value.CompareTo(right.Value);
    }
}
