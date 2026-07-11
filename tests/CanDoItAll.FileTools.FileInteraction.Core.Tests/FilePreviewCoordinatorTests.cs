namespace CanDoItAll.FileTools.FileInteraction.Core.Tests;

public sealed class FilePreviewCoordinatorTests
{
    [Fact]
    public async Task RequestAsync_RapidEdits_CoalescesBeforeGeneration()
    {
        var delay = new ManualFileInteractionDelay();
        var generator = new ImmediatePreviewGenerator();
        await using var coordinator = new FilePreviewCoordinator<string>(
            generator,
            TimeSpan.FromMilliseconds(300),
            delay);
        var first = coordinator.RequestAsync(Snapshot(1)).AsTask();
        var second = coordinator.RequestAsync(Snapshot(2)).AsTask();

        Assert.Equal(1, delay.ActiveCount);
        delay.ReleaseNext();

        Assert.Null(await first);
        var update = Assert.IsType<FilePreviewUpdate<string>>(await second);
        Assert.Equal(2, update.EditRevision);
        Assert.Equal(1, generator.Count);
        Assert.Equal(2, coordinator.Current?.EditRevision);
    }

    [Fact]
    public async Task RequestAsync_OlderGeneratorIgnoresCancellation_StaleCompletionIsRejected()
    {
        var generator = new ControlledPreviewGenerator();
        await using var coordinator = new FilePreviewCoordinator<string>(
            generator,
            TimeSpan.Zero,
            ImmediateFileInteractionDelay.Instance);
        var first = coordinator.RequestAsync(Snapshot(1)).AsTask();
        var second = coordinator.RequestAsync(Snapshot(2)).AsTask();
        await TestWait.UntilAsync(() => generator.Count == 2);

        generator.Complete(2, "new");
        Assert.Equal(2, (await second)?.EditRevision);
        generator.Complete(1, "old");

        Assert.Null(await first);
        Assert.Equal("new", coordinator.Current?.Preview);
        Assert.Equal(2, coordinator.Current?.EditRevision);
    }

    [Fact]
    public async Task RequestAsync_PublishesMetadataFromTheSameImmutableEditSnapshot()
    {
        await using var coordinator = new FilePreviewCoordinator<string>(
            new ImmediatePreviewGenerator(),
            TimeSpan.Zero,
            ImmediateFileInteractionDelay.Instance);
        var snapshot = new FileEditSnapshot(
            FileEditSessionTests.File(),
            7,
            FileEditSessionTests.Bytes("content"),
            "application/x-specialized",
            "utf-16");

        var update = Assert.IsType<FilePreviewUpdate<string>>(
            await coordinator.RequestAsync(snapshot));

        Assert.Equal("application/x-specialized", update.MediaType);
        Assert.Equal("utf-16", update.EncodingName);
        Assert.Equal(7, update.EditRevision);
    }

    [Fact]
    public void Constructor_NegativeDebounce_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FilePreviewCoordinator<string>(
            new ImmediatePreviewGenerator(),
            TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public async Task Dispose_PendingDelay_CompletesRequestWithoutPreview()
    {
        var delay = new ManualFileInteractionDelay();
        var generator = new ImmediatePreviewGenerator();
        var coordinator = new FilePreviewCoordinator<string>(generator, TimeSpan.FromSeconds(1), delay);
        var request = coordinator.RequestAsync(Snapshot(1)).AsTask();

        await coordinator.DisposeAsync();

        Assert.Null(await request);
        Assert.Equal(0, generator.Count);
    }

    [Fact]
    public async Task Dispose_GeneratorIgnoresCancellation_DrainsWorkWithoutPublishingIt()
    {
        var generator = new ControlledPreviewGenerator();
        var coordinator = new FilePreviewCoordinator<string>(
            generator,
            TimeSpan.Zero,
            ImmediateFileInteractionDelay.Instance);
        var request = coordinator.RequestAsync(Snapshot(1)).AsTask();
        await TestWait.UntilAsync(() => generator.Count == 1);

        var disposal = coordinator.DisposeAsync().AsTask();

        Assert.False(disposal.IsCompleted);
        generator.Complete(1, "late");
        await disposal;
        Assert.Null(await request);
        Assert.Null(coordinator.Current);
    }

    private static FileEditSnapshot Snapshot(long revision)
        => new(FileEditSessionTests.File(), revision, FileEditSessionTests.Bytes($"v{revision}"), "text/plain");
}

internal sealed class ImmediatePreviewGenerator : IFilePreviewGenerator<string>
{
    public int Count { get; private set; }

    public ValueTask<string> GenerateAsync(FileEditSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        Count++;
        return ValueTask.FromResult(Encoding.UTF8.GetString(snapshot.Content.Span));
    }
}

internal sealed class ControlledPreviewGenerator : IFilePreviewGenerator<string>
{
    private readonly Dictionary<long, TaskCompletionSource<string>> completions = [];

    public int Count => completions.Count;

    public ValueTask<string> GenerateAsync(FileEditSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        completions.Add(snapshot.EditRevision, completion);
        return new ValueTask<string>(completion.Task);
    }

    public void Complete(long revision, string value) => completions[revision].SetResult(value);
}

internal sealed class ImmediateFileInteractionDelay : IFileInteractionDelay
{
    public static ImmediateFileInteractionDelay Instance { get; } = new();

    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
