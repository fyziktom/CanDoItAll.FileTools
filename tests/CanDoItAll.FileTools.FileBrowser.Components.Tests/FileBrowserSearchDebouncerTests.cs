namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

public sealed class FileBrowserSearchDebouncerTests
{
    [Fact]
    public async Task LaterEdit_SupersedesPendingCallback()
    {
        await using var debouncer = new FileBrowserSearchDebouncer();
        int firstCalls = 0;
        int secondCalls = 0;

        Task first = debouncer.ScheduleAsync(
            TimeSpan.FromSeconds(2),
            _ =>
            {
                firstCalls++;
                return ValueTask.CompletedTask;
            },
            CancellationToken.None).AsTask();
        await Task.Delay(20);
        Task second = debouncer.ScheduleAsync(
            TimeSpan.Zero,
            _ =>
            {
                secondCalls++;
                return ValueTask.CompletedTask;
            },
            CancellationToken.None).AsTask();

        await Task.WhenAll(first, second);

        Assert.Equal(0, firstCalls);
        Assert.Equal(1, secondCalls);
        Assert.False(debouncer.HasPending);
    }

    [Fact]
    public async Task Cancel_PreventsPendingCallback()
    {
        await using var debouncer = new FileBrowserSearchDebouncer();
        bool called = false;
        Task pending = debouncer.ScheduleAsync(
            TimeSpan.FromSeconds(2),
            _ =>
            {
                called = true;
                return ValueTask.CompletedTask;
            },
            CancellationToken.None).AsTask();

        debouncer.Cancel();
        await pending;

        Assert.False(called);
        Assert.False(debouncer.HasPending);
    }
}
