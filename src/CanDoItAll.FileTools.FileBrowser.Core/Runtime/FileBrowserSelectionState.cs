namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Owns single/toggle selection independently from session I/O and rendering.</summary>
public sealed class FileBrowserSelectionState
{
    private readonly object sync = new();
    private readonly HashSet<FileBrowserItemKey> selectedKeys = [];

    public IReadOnlySet<FileBrowserItemKey> Snapshot()
    {
        lock (sync)
        {
            return selectedKeys.ToHashSet();
        }
    }

    public void Select(
        IReadOnlyList<FileBrowserItem> visibleItems,
        FileBrowserItemKey itemKey,
        bool toggle = false)
    {
        ArgumentNullException.ThrowIfNull(visibleItems);
        if (!visibleItems.Any(item => item.Key == itemKey))
        {
            throw new ArgumentException("Only a currently visible item can be selected.", nameof(itemKey));
        }

        lock (sync)
        {
            if (toggle)
            {
                if (!selectedKeys.Add(itemKey))
                {
                    selectedKeys.Remove(itemKey);
                }
            }
            else
            {
                selectedKeys.Clear();
                selectedKeys.Add(itemKey);
            }
        }
    }

    public bool Clear()
    {
        lock (sync)
        {
            if (selectedKeys.Count == 0)
            {
                return false;
            }

            selectedKeys.Clear();
            return true;
        }
    }

    public void Reconcile(IReadOnlyList<FileBrowserItem> visibleItems)
    {
        ArgumentNullException.ThrowIfNull(visibleItems);
        HashSet<FileBrowserItemKey> visible = visibleItems.Select(item => item.Key).ToHashSet();
        lock (sync)
        {
            selectedKeys.RemoveWhere(key => !visible.Contains(key));
        }
    }

    internal void Restore(IReadOnlySet<FileBrowserItemKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (sync)
        {
            selectedKeys.Clear();
            selectedKeys.UnionWith(keys);
        }
    }
}
