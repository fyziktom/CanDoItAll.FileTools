namespace CanDoItAll.FileTools.FileBrowser.Components;

internal static class FileBrowserInteractionPolicy
{
    public static bool CanSelect(FileBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Supports(FileBrowserItemCapabilities.Select);
    }

    public static bool CanActivate(FileBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsContainer
            ? item.Supports(FileBrowserItemCapabilities.Navigate)
                || item.Supports(FileBrowserItemCapabilities.Open)
            : item.Supports(FileBrowserItemCapabilities.Open)
                || item.Supports(FileBrowserItemCapabilities.Preview);
    }

    public static bool NavigatesInternally(FileBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsContainer && item.Supports(FileBrowserItemCapabilities.Navigate);
    }

    public static string BuildMainLabel(FileBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool canSelect = CanSelect(item);
        bool canActivate = CanActivate(item);
        return (canSelect, canActivate) switch
        {
            (true, true) => $"Select {item.Name}; press Enter or double-click to open",
            (true, false) => $"Select {item.Name}",
            (false, true) => $"Open {item.Name}",
            _ => item.Name
        };
    }

    public static bool IsActionSupported(
        FileBrowserItem item,
        FileBrowserSourceDescriptor? source,
        string actionId)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (FileBrowserBuiltInActions.GetFor(item)
            .Any(action => string.Equals(action.Id, actionId, StringComparison.Ordinal)))
        {
            return true;
        }

        return !FileBrowserActionIds.IsReserved(actionId)
            && item.Supports(FileBrowserItemCapabilities.CustomActions)
            && source?.Supports(FileBrowserSourceCapabilities.CustomActions) == true;
    }
}
