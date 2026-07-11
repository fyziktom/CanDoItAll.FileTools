namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

public sealed class FileBrowserInteractionGuardTests
{
    [Fact]
    public void SnapshotAndSessionChanges_InvalidateCapturedWork()
    {
        var guard = new FileBrowserInteractionGuard();
        FileBrowserInteractionStamp initial = guard.Capture();

        Assert.True(guard.IsCurrent(initial));
        guard.AcceptSnapshot();
        Assert.False(guard.IsCurrent(initial));

        FileBrowserInteractionStamp afterSnapshot = guard.Capture();
        guard.ChangeSession();
        Assert.False(guard.IsCurrent(afterSnapshot));
        Assert.False(guard.IsCurrentSession(afterSnapshot.SessionVersion));
    }
}
