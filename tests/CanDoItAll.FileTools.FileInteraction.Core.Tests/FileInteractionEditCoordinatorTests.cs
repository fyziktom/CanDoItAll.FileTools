namespace CanDoItAll.FileTools.FileInteraction.Core.Tests;

public sealed class FileInteractionEditCoordinatorTests
{
    [Fact]
    public async Task UndoRedo_AppliesMonotonicDirtyRevisions_AndPreservesBranch()
    {
        var history = new BoundedTextHistoryProvider(new FileHistoryOptions(10, 1_000));
        await using var coordinator = await FileInteractionEditCoordinator.CreateAsync(
            Initial("a"),
            new FileContentRevision("base-0"),
            new ControlledSaveTarget(),
            history);
        await coordinator.ApplyEditAsync(Bytes("b"));
        await coordinator.ApplyEditAsync(Bytes("c"));

        var undone = await coordinator.UndoAsync();

        Assert.Equal(3, undone?.EditRevision);
        Assert.Equal("b", Text(undone));
        Assert.True(coordinator.State.IsDirty);
        Assert.True(coordinator.HistoryState.CanRedo);

        var redone = await coordinator.RedoAsync();

        Assert.Equal(4, redone?.EditRevision);
        Assert.Equal("c", Text(redone));
        Assert.False(coordinator.HistoryState.CanRedo);
    }

    [Fact]
    public async Task EditAfterUndo_TruncatesRedo_WithoutRecordingUndoAsNewBranch()
    {
        var history = new BoundedTextHistoryProvider(new FileHistoryOptions(10, 1_000));
        await using var coordinator = await FileInteractionEditCoordinator.CreateAsync(
            Initial("a"),
            null,
            new ControlledSaveTarget(),
            history);
        await coordinator.ApplyEditAsync(Bytes("b"));
        await coordinator.ApplyEditAsync(Bytes("c"));
        _ = await coordinator.UndoAsync();

        var branched = await coordinator.ApplyEditAsync(Bytes("d"));

        Assert.Equal(4, branched.EditRevision);
        Assert.False(coordinator.HistoryState.CanRedo);
        Assert.Null(await coordinator.RedoAsync());
        Assert.Equal("b", Text(await coordinator.UndoAsync()));
    }

    [Fact]
    public async Task ResolveConflictByRebasing_ResetsHistoryAtNewBaseRevision()
    {
        var target = new ControlledSaveTarget();
        var history = new BoundedTextHistoryProvider(new FileHistoryOptions(10, 1_000));
        await using var coordinator = await FileInteractionEditCoordinator.CreateAsync(
            Initial("a"),
            new FileContentRevision("base-0"),
            target,
            history);
        await coordinator.ApplyEditAsync(Bytes("b"));
        var save = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Fail(0, new FileSaveConflictException(
            FileEditSessionTests.File(),
            new FileContentRevision("base-0"),
            new FileContentRevision("external")));
        await save;

        await coordinator.ResolveConflictByRebasingAsync(new FileContentRevision("external"));

        Assert.False(coordinator.State.HasConflict);
        Assert.Equal("external", coordinator.State.BaseRevision?.Value);
        Assert.Equal(default, coordinator.HistoryState);
    }

    [Fact]
    public async Task CreateWithoutHistory_DisablesUndoAndRedoCleanly()
    {
        await using var coordinator = await FileInteractionEditCoordinator.CreateAsync(
            Initial("a"),
            null,
            new ControlledSaveTarget());

        Assert.Equal(default, coordinator.HistoryState);
        Assert.Null(await coordinator.UndoAsync());
        Assert.Null(await coordinator.RedoAsync());
    }

    private static FileEditSnapshot Initial(string text)
        => new(FileEditSessionTests.File(), 0, Bytes(text), "text/plain", "utf-8");

    private static ReadOnlyMemory<byte> Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string? Text(FileEditSnapshot? snapshot)
        => snapshot is null ? null : Encoding.UTF8.GetString(snapshot.Content.Span);
}
