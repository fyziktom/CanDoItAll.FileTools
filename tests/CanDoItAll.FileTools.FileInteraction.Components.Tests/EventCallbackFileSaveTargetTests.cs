using System.Text;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class EventCallbackFileSaveTargetTests
{
    [Fact]
    public async Task SaveAsync_AwaitsHostAndReturnsAcknowledgedRevision()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<string>();
        var callback = EventCallback.Factory.Create<FileInteractionSaveRequestedEventArgs>(
            new object(),
            async args =>
            {
                events.Add("host");
                entered.SetResult();
                await release.Task;
                args.SetPersistedRevision(new FileContentRevision("r2"));
            });
        var target = new EventCallbackFileSaveTarget(
            () => callback,
            _ => { events.Add("starting"); return Task.CompletedTask; });

        var save = target.SaveAsync(Request()).AsTask();
        await entered.Task;
        Assert.False(save.IsCompleted);
        release.SetResult();

        var result = await save;

        Assert.Equal("r2", result.PersistedRevision?.Value);
        Assert.Equal(["starting", "host"], events);
    }

    [Fact]
    public async Task SaveAsync_HostConflictIsReportedAndPropagated()
    {
        var file = new FileReference("test", "file.txt");
        var conflict = new FileSaveConflictException(
            file,
            new FileContentRevision("r1"),
            new FileContentRevision("r2"));
        var callback = EventCallback.Factory.Create<FileInteractionSaveRequestedEventArgs>(
            new object(),
            (FileInteractionSaveRequestedEventArgs _) => throw conflict);
        var target = new EventCallbackFileSaveTarget(
            () => callback,
            _ => Task.CompletedTask);

        var thrown = await Assert.ThrowsAsync<FileSaveConflictException>(async () =>
            await target.SaveAsync(Request(file)));

        Assert.Same(conflict, thrown);
    }

    [Fact]
    public async Task SaveAsync_MissingHostCallbackFailsInsteadOfPretendingToPersist()
    {
        var target = new EventCallbackFileSaveTarget(
            () => default,
            _ => Task.CompletedTask);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await target.SaveAsync(Request()));

        Assert.Contains("save callback", error.Message, StringComparison.Ordinal);
    }

    private static FileSaveRequest Request(FileReference? file = null)
        => new(
            file ?? new FileReference("test", "file.txt"),
            editRevision: 1,
            new BufferedFileSaveContent(Encoding.UTF8.GetBytes("changed")),
            new FileContentRevision("r1"),
            "text/plain",
            "utf-8");
}
