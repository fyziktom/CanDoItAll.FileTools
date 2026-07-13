using System.Globalization;

namespace CanDoItAll.FileTools.FileBrowser.Components;

/// <summary>Controls the amount of browser chrome rendered for a host surface.</summary>
public enum FileBrowserDisplayMode
{
    /// <summary>Full navigation, filtering, metadata, and status UI.</summary>
    Standard,

    /// <summary>Dense UI suitable for medium floating panels and dialogs.</summary>
    Compact,

    /// <summary>Essential navigation and actions for narrow or short-lived surfaces.</summary>
    Minimal
}

/// <summary>Available projections of the same browser session snapshot.</summary>
public enum FileBrowserViewMode
{
    List,
    Cards
}

/// <summary>Reason an item was activated rather than only selected.</summary>
public enum FileBrowserInvocationKind
{
    /// <summary>A primary button activation, including pointer, touch, or native Space activation.</summary>
    PrimaryAction,

    PointerDoubleClick,
    Keyboard
}

/// <summary>Host notification for a file or non-navigable item activation.</summary>
public sealed record FileBrowserItemInvokedEventArgs(
    FileBrowserItem Item,
    FileBrowserInvocationKind Kind);

/// <summary>
/// Host notification for a capability-driven item action. The component never executes the
/// action result itself; the host decides whether and how to open, copy, download, or run it.
/// </summary>
public sealed record FileBrowserItemActionEventArgs(
    FileBrowserItem Item,
    FileBrowserActionDescriptor Action)
{
    public string ActionId => Action.Id;

    public FileBrowserActionRequest CreateRequest()
        => new(Item.Key, Action.Id);
}

/// <summary>Formatting helpers shared by list and card projections.</summary>
public static class FileBrowserDisplayFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string FormatSize(long? bytes)
    {
        if (!bytes.HasValue)
        {
            return "\u2014";
        }

        double value = bytes.Value;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{bytes.Value:N0} {Units[unit]}")
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {Units[unit]}");
    }

    public static string FormatDate(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "\u2014";

    public static string FormatType(FileBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsContainer
            ? "Folder"
            : item.MediaType ?? item.Category.ToString();
    }

    public static string FormatScope(FileBrowserSearchScope scope)
        => scope switch
        {
            FileBrowserSearchScope.LoadedFolder => "This loaded folder",
            FileBrowserSearchScope.LoadedDescendants => "Loaded descendants",
            FileBrowserSearchScope.Provider => "All source items",
            _ => "Progressive deep search"
        };
}

/// <summary>Maps provider-neutral item categories to compact, dependency-free symbols.</summary>
public static class FileBrowserIconResolver
{
    public static string Resolve(FileBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Category switch
        {
            FileBrowserItemCategory.Folder => "\ud83d\udcc1",
            FileBrowserItemCategory.Image => "\ud83d\uddbc",
            FileBrowserItemCategory.Video => "\u25b6",
            FileBrowserItemCategory.Audio => "\u266b",
            FileBrowserItemCategory.Archive => "\u25a3",
            FileBrowserItemCategory.Code => "</>",
            FileBrowserItemCategory.Data => "#",
            FileBrowserItemCategory.Link => "\u2197",
            FileBrowserItemCategory.Document => "\u2261",
            _ => "\u25a1"
        };
    }
}
