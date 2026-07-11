namespace CanDoItAll.FileTools.FileInteraction.Core.Tests;

public sealed class BoundedTextHistoryProviderTests
{
    [Fact]
    public async Task RecordAfterUndo_TruncatesRedoBranch()
    {
        await using var history = History(entries: 10, bytes: 100);
        await history.ResetAsync(File(), Revision("base"), Snapshot(0, "a"));
        await history.RecordAsync(Snapshot(1, "b"));
        await history.RecordAsync(Snapshot(2, "c"));

        Assert.Equal("b", Text(await history.UndoAsync()));
        await history.RecordAsync(Snapshot(3, "d"));

        Assert.False(history.State.CanRedo);
        Assert.Null(await history.RedoAsync());
        Assert.Equal("b", Text(await history.UndoAsync()));
    }

    [Fact]
    public async Task Record_EntryLimit_EvictsOldestUndoSnapshots()
    {
        await using var history = History(entries: 2, bytes: 100);
        await history.ResetAsync(File(), null, Snapshot(0, "a"));
        await history.RecordAsync(Snapshot(1, "b"));
        await history.RecordAsync(Snapshot(2, "c"));
        await history.RecordAsync(Snapshot(3, "d"));

        Assert.Equal(2, history.State.UndoDepth);
        Assert.Equal("c", Text(await history.UndoAsync()));
        Assert.Equal("b", Text(await history.UndoAsync()));
        Assert.Null(await history.UndoAsync());
    }

    [Fact]
    public async Task Record_ByteLimit_EvictsSnapshotsUntilWithinBudget()
    {
        await using var history = History(entries: 10, bytes: 3);
        await history.ResetAsync(File(), null, Snapshot(0, "aa"));
        await history.RecordAsync(Snapshot(1, "bb"));
        await history.RecordAsync(Snapshot(2, "cc"));

        Assert.Equal(1, history.State.UndoDepth);
        Assert.Equal("bb", Text(await history.UndoAsync()));
        Assert.Null(await history.UndoAsync());
    }

    [Fact]
    public async Task Reset_DifferentFileAndBaseRevision_ClearsBothBranches()
    {
        await using var history = History();
        await history.ResetAsync(File(), Revision("base-1"), Snapshot(0, "a"));
        await history.RecordAsync(Snapshot(1, "b"));
        _ = await history.UndoAsync();
        var otherFile = new FileReference("test", "other");
        var otherSnapshot = new FileEditSnapshot(otherFile, 9, FileEditSessionTests.Bytes("x"));

        await history.ResetAsync(otherFile, Revision("base-2"), otherSnapshot);

        Assert.Equal(otherFile, history.File);
        Assert.Equal("base-2", history.BaseRevision?.Value);
        Assert.Equal(default, history.State);
    }

    [Fact]
    public async Task Reset_SameFileWithNewBaseRevision_ClearsExistingUndoHistory()
    {
        await using var history = History();
        await history.ResetAsync(File(), Revision("base-1"), Snapshot(0, "a"));
        await history.RecordAsync(Snapshot(1, "b"));

        await history.ResetAsync(File(), Revision("base-2"), Snapshot(10, "external"));

        Assert.Equal("base-2", history.BaseRevision?.Value);
        Assert.Equal(default, history.State);
        Assert.Null(await history.UndoAsync());
    }

    [Fact]
    public async Task Record_DifferentFile_ThrowsWithoutChangingState()
    {
        await using var history = History();
        await history.ResetAsync(File(), null, Snapshot(0, "a"));
        var other = new FileEditSnapshot(new FileReference("test", "other"), 1, FileEditSessionTests.Bytes("b"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => history.RecordAsync(other).AsTask());

        Assert.Equal(default, history.State);
    }

    [Fact]
    public async Task Record_NonIncreasingRevision_ThrowsAfterUndo()
    {
        await using var history = History();
        await history.ResetAsync(File(), null, Snapshot(0, "a"));
        await history.RecordAsync(Snapshot(1, "b"));
        _ = await history.UndoAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => history.RecordAsync(Snapshot(1, "branch")).AsTask());
    }

    [Fact]
    public void Constructor_DisabledLimits_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BoundedTextHistoryProvider(FileHistoryOptions.Disabled));
    }

    [Fact]
    public async Task UseBeforeReset_Throws()
    {
        await using var history = History();

        await Assert.ThrowsAsync<InvalidOperationException>(() => history.UndoAsync().AsTask());
    }

    private static BoundedTextHistoryProvider History(int entries = 10, long bytes = 100)
        => new(new FileHistoryOptions(entries, bytes));

    private static FileReference File() => FileEditSessionTests.File();

    private static FileContentRevision Revision(string value) => new(value);

    private static FileEditSnapshot Snapshot(long revision, string value)
        => new(File(), revision, FileEditSessionTests.Bytes(value), "text/plain", "utf-8");

    private static string? Text(FileEditSnapshot? snapshot)
        => snapshot is null ? null : Encoding.UTF8.GetString(snapshot.Content.Span);
}
