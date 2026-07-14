namespace CanDoItAll.FileTools.FileBrowser.Components;

public sealed record FileBrowserHostActionContext
{
    public FileBrowserHostActionContext(
        FileBrowserItem item,
        FileBrowserSourceDescriptor? source,
        long snapshotRevision)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        if (snapshotRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshotRevision));
        }

        Source = source;
        SnapshotRevision = snapshotRevision;
    }

    public FileBrowserItem Item { get; }

    public FileBrowserSourceDescriptor? Source { get; }

    public long SnapshotRevision { get; }
}

public interface IFileBrowserHostActionCatalog
{
    ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
        FileBrowserHostActionContext context,
        CancellationToken cancellationToken = default);
}

internal sealed record FileBrowserPresentedAction(
    FileBrowserActionDescriptor Action,
    FileBrowserActionOrigin Origin);

internal static class FileBrowserPresentedActionCatalog
{
    public static IReadOnlyList<FileBrowserPresentedAction> Merge(
        IReadOnlyList<FileBrowserActionDescriptor>? sessionActions,
        IReadOnlyList<FileBrowserActionDescriptor>? hostActions)
    {
        if (sessionActions is null)
        {
            throw new InvalidOperationException("The browser session returned a null action collection.");
        }

        if (hostActions is null)
        {
            throw new InvalidOperationException("The host action catalog returned a null action collection.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<FileBrowserPresentedAction>(sessionActions.Count + hostActions.Count);
        Add(sessionActions, FileBrowserActionOrigin.Session, ids, merged);
        Add(hostActions, FileBrowserActionOrigin.Host, ids, merged);
        return merged.AsReadOnly();
    }

    private static void Add(
        IReadOnlyList<FileBrowserActionDescriptor> actions,
        FileBrowserActionOrigin origin,
        HashSet<string> ids,
        List<FileBrowserPresentedAction> merged)
    {
        foreach (FileBrowserActionDescriptor action in actions)
        {
            if (action is null)
            {
                throw new InvalidOperationException("Action catalogs cannot contain null actions.");
            }

            if (!ids.Add(action.Id))
            {
                throw new InvalidOperationException($"Duplicate action identifier '{action.Id}'.");
            }

            merged.Add(new FileBrowserPresentedAction(action, origin));
        }
    }
}
