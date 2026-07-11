namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>A resolved root-to-container path used by navigation and breadcrumbs.</summary>
public sealed record FileBrowserLocation
{
    public FileBrowserLocation(IReadOnlyList<FileBrowserItem> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Count == 0)
        {
            throw new ArgumentException("A browser location requires a non-empty path.", nameof(path));
        }

        if (path.Any(item => !item.IsContainer))
        {
            throw new ArgumentException("Every item in a browser location must be a container.", nameof(path));
        }

        var sourceId = path[0].Key.SourceId;
        if (path.Any(item => item.Key.SourceId != sourceId))
        {
            throw new ArgumentException("Every item in a browser location must belong to one source.", nameof(path));
        }

        Path = Array.AsReadOnly(path.ToArray());
    }

    public IReadOnlyList<FileBrowserItem> Path { get; }

    public FileBrowserItem Current => Path[^1];

    public FileBrowserItemKey Key => Current.Key;

    public bool CanGoUp => Path.Count > 1;

    public FileBrowserLocation Parent()
        => CanGoUp
            ? new FileBrowserLocation(Path.Take(Path.Count - 1).ToArray())
            : this;
}

/// <summary>Owns back, forward, and up history independently from provider I/O.</summary>
public sealed class FileBrowserNavigationState
{
    private readonly Stack<FileBrowserLocation> back = [];
    private readonly Stack<FileBrowserLocation> forward = [];

    public FileBrowserLocation? Current { get; private set; }

    public bool CanGoBack => back.Count > 0;

    public bool CanGoForward => forward.Count > 0;

    public bool CanGoUp => Current?.CanGoUp == true;

    internal FileBrowserNavigationCheckpoint Capture()
        => new(Current, back.ToArray(), forward.ToArray());

    internal void Restore(FileBrowserNavigationCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        Current = checkpoint.Current;
        RestoreStack(back, checkpoint.Back);
        RestoreStack(forward, checkpoint.Forward);
    }

    public void Reset(FileBrowserLocation location)
    {
        Current = location ?? throw new ArgumentNullException(nameof(location));
        back.Clear();
        forward.Clear();
    }

    public void Clear()
    {
        Current = null;
        back.Clear();
        forward.Clear();
    }

    public void Navigate(FileBrowserLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (Current is null)
        {
            Reset(location);
            return;
        }

        if (Current.Key == location.Key)
        {
            Current = location;
            return;
        }

        back.Push(Current);
        Current = location;
        forward.Clear();
    }

    public FileBrowserLocation GoBack()
    {
        _ = PeekBack();

        forward.Push(Current!);
        Current = back.Pop();
        return Current;
    }

    internal FileBrowserLocation PeekBack()
        => Current is null || back.Count == 0
            ? throw new InvalidOperationException("There is no previous browser location.")
            : back.Peek();

    public FileBrowserLocation GoForward()
    {
        _ = PeekForward();

        back.Push(Current!);
        Current = forward.Pop();
        return Current;
    }

    internal FileBrowserLocation PeekForward()
        => Current is null || forward.Count == 0
            ? throw new InvalidOperationException("There is no forward browser location.")
            : forward.Peek();

    public FileBrowserLocation GoUp()
    {
        var parent = PeekUp();

        back.Push(Current!);
        Current = parent;
        forward.Clear();
        return Current;
    }

    internal FileBrowserLocation PeekUp()
        => Current is null || !Current.CanGoUp
            ? throw new InvalidOperationException("The current browser location has no parent.")
            : Current.Parent();

    public void ReplaceCurrent(FileBrowserLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (Current is null || Current.Key != location.Key)
        {
            throw new InvalidOperationException("Only the current location path can be replaced without navigation.");
        }

        Current = location;
    }

    private static void RestoreStack(
        Stack<FileBrowserLocation> destination,
        IReadOnlyList<FileBrowserLocation> topFirst)
    {
        destination.Clear();
        for (var index = topFirst.Count - 1; index >= 0; index--)
        {
            destination.Push(topFirst[index]);
        }
    }
}

internal sealed record FileBrowserNavigationCheckpoint(
    FileBrowserLocation? Current,
    IReadOnlyList<FileBrowserLocation> Back,
    IReadOnlyList<FileBrowserLocation> Forward);

