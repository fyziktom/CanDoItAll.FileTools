namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class FileBrowserSourceRevisionGuardTests
{
    [Fact]
    public async Task RetiredGeneration_DisposesOnlyAfterItsDependentLeaseDrains()
    {
        using var guard = new FileBrowserSourceRevisionGuard();
        FileBrowserSourceRevision dependentLease = guard.Capture();
        FileBrowserSourceRevisionChange change = guard.Supersede();

        await guard.CancelRetiredAsync(change);

        Assert.Equal(1, guard.RetiredGenerationCount);
        Assert.Equal(0, guard.DisposedGenerationCount);
        guard.Release(dependentLease);
        Assert.Equal(0, guard.RetiredGenerationCount);
        Assert.Equal(1, guard.DisposedGenerationCount);
    }

    [Fact]
    public async Task RepeatedSupersession_DoesNotAccumulateDrainedCancellationSources()
    {
        using var guard = new FileBrowserSourceRevisionGuard();

        for (var index = 0; index < 100; index++)
        {
            FileBrowserSourceRevisionChange change = guard.Supersede();
            await guard.CancelRetiredAsync(change);
        }

        Assert.Equal(0, guard.RetiredGenerationCount);
        Assert.Equal(100, guard.DisposedGenerationCount);
    }
}
