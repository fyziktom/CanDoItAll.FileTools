namespace CanDoItAll.FileTools.FileBrowser.Components;

internal readonly record struct FileBrowserInteractionStamp(long SessionVersion, long SnapshotVersion);

/// <summary>Rejects stale asynchronous UI completions after session or snapshot changes.</summary>
internal sealed class FileBrowserInteractionGuard
{
    private readonly Lock gate = new();
    private long sessionVersion;
    private long snapshotVersion;

    public FileBrowserInteractionStamp ChangeSession()
    {
        lock (gate)
        {
            sessionVersion++;
            snapshotVersion++;
            return new FileBrowserInteractionStamp(sessionVersion, snapshotVersion);
        }
    }

    public FileBrowserInteractionStamp AcceptSnapshot()
    {
        lock (gate)
        {
            snapshotVersion++;
            return new FileBrowserInteractionStamp(sessionVersion, snapshotVersion);
        }
    }

    public FileBrowserInteractionStamp Capture()
    {
        lock (gate)
        {
            return new FileBrowserInteractionStamp(sessionVersion, snapshotVersion);
        }
    }

    public bool IsCurrent(FileBrowserInteractionStamp stamp)
    {
        lock (gate)
        {
            return stamp.SessionVersion == sessionVersion
                && stamp.SnapshotVersion == snapshotVersion;
        }
    }

    public bool IsCurrentSession(long expectedSessionVersion)
    {
        lock (gate)
        {
            return expectedSessionVersion == sessionVersion;
        }
    }
}
