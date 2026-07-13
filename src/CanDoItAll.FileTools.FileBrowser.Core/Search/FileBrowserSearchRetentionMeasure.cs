using System.Text;

namespace CanDoItAll.FileTools.FileBrowser;

internal static class FileBrowserSearchRetentionMeasure
{
    private const long ItemOverheadBytes = 256;
    private const long MetadataEntryOverheadBytes = 32;

    public static long Measure(IEnumerable<FileBrowserItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        long bytes = 0;
        foreach (FileBrowserItem item in items)
        {
            bytes = Add(bytes, Measure(item));
        }

        return bytes;
    }

    public static long Measure(FileBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        long bytes = ItemOverheadBytes;
        bytes = Add(bytes, Measure(item.Key));
        bytes = Add(bytes, item.ParentKey.HasValue ? Measure(item.ParentKey.Value) : 0);
        bytes = Add(bytes, Measure(item.Name));
        bytes = Add(bytes, Measure(item.DisplayPath));
        bytes = Add(bytes, Measure(item.MediaType));
        bytes = Add(bytes, Measure(item.Owner));
        bytes = Add(bytes, Measure(item.OpenUri));
        bytes = Add(bytes, Measure(item.DownloadUri));
        bytes = Add(bytes, Measure(item.ContentIdentity?.Scheme));
        bytes = Add(bytes, Measure(item.ContentIdentity?.Value));
        foreach ((string key, string value) in item.Metadata)
        {
            bytes = Add(bytes, MetadataEntryOverheadBytes);
            bytes = Add(bytes, Measure(key));
            bytes = Add(bytes, Measure(value));
        }

        return bytes;
    }

    private static long Measure(FileBrowserItemKey key)
        => Add(
            Add(Measure(key.SourceId.Value), Measure(key.Value)),
            Measure(key.Revision));

    private static int Measure(string? value)
        => value is null ? 0 : Encoding.UTF8.GetByteCount(value);

    private static long Add(long left, long right)
        => left > long.MaxValue - right ? long.MaxValue : left + right;
}
