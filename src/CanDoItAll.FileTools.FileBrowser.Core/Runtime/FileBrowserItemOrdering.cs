namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Creates the deterministic ordering used by providers, mocks, and loaded searches.</summary>
public sealed class FileBrowserItemComparer : IComparer<FileBrowserItem>
{
    private readonly FileBrowserSortDescriptor sort;

    public FileBrowserItemComparer(FileBrowserSortDescriptor? sort = null)
    {
        this.sort = sort ?? new FileBrowserSortDescriptor();
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

        if (sort.Field == FileBrowserSortField.ProviderNative)
        {
            return 0;
        }

        if (sort.FoldersFirst && x.IsContainer != y.IsContainer)
        {
            return x.IsContainer ? -1 : 1;
        }

        var result = sort.Field switch
        {
            FileBrowserSortField.ModifiedAt => CompareNullable(x.ModifiedAt, y.ModifiedAt),
            FileBrowserSortField.Size => CompareNullable(x.Size, y.Size),
            FileBrowserSortField.Type => CompareText(GetTypeValue(x), GetTypeValue(y)),
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

        if (result == 0)
        {
            result = StringComparer.Ordinal.Compare(x.Key.ToString(), y.Key.ToString());
        }

        return result;
    }

    private static string GetTypeValue(FileBrowserItem item)
        => item.MediaType ?? item.Category.ToString();

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

/// <summary>Convenience ordering extensions shared by adapters and strategies.</summary>
public static class FileBrowserItemOrdering
{
    public static IReadOnlyList<FileBrowserItem> Apply(
        IEnumerable<FileBrowserItem> items,
        FileBrowserSortDescriptor? sort = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (sort?.Field == FileBrowserSortField.ProviderNative)
        {
            return items.ToArray();
        }

        return items.Order(new FileBrowserItemComparer(sort)).ToArray();
    }
}

