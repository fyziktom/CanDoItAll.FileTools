namespace CanDoItAll.FileTools.FileBrowser.Tests;

public sealed class FileBrowserSessionExecutionCoordinatorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RetiredCommand_CancelsBeforeLinkedTokenPropagation(bool superseding, bool providerFails)
    {
        var published = new List<FileBrowserOperationKind>();
        await using var execution = CreateExecution(published);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken actionToken = default;
        async ValueTask Action(CancellationToken token)
        {
            actionToken = token;
            entered.TrySetResult();
            await release.Task;
            if (providerFails)
            {
                throw new InvalidOperationException("The retired provider failed.");
            }
        }

        Task command = (superseding
            ? execution.ExecuteSupersedingAsync(FileBrowserOperationKind.Initializing, Action, true, true, default)
            : execution.ExecuteAsync(FileBrowserOperationKind.Initializing, Action, default)).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        FileBrowserSourceRevisionChange change = execution.SourceRevisions.Supersede();
        published.Clear();
        try
        {
            Assert.False(actionToken.IsCancellationRequested);
            release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command);
            Assert.Empty(published);
            Assert.Equal(FileBrowserOperationKind.Idle, execution.Operation);
            Assert.Null(execution.Error);
        }
        finally
        {
            release.TrySetResult();
            await execution.SourceRevisions.CancelRetiredAsync(change);
        }
    }

    [Fact]
    public async Task RetiredSerializedResult_IsDisposedBeforeCancellationPropagation()
    {
        await using var execution = CreateExecution([]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TrackedResult();
        CancellationToken actionToken = default;
        Task<TrackedResult> command = execution.ExecuteSerializedAsync(async token =>
        {
            actionToken = token;
            entered.TrySetResult();
            await release.Task;
            return result;
        }, default).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        FileBrowserSourceRevisionChange change = execution.SourceRevisions.Supersede();
        try
        {
            Assert.False(actionToken.IsCancellationRequested);
            release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command);
            Assert.Equal(1, result.DisposeCount);
        }
        finally
        {
            release.TrySetResult();
            await execution.SourceRevisions.CancelRetiredAsync(change);
        }
    }

    private static FileBrowserSessionExecutionCoordinator CreateExecution(List<FileBrowserOperationKind> published)
    {
        var options = new FileBrowserSessionOptions();
        var runtime = new FileBrowserSessionRuntime(
            new FileBrowserSourceSet("initial", []), null, options,
            FileBrowserStateStoreFactory.Create(options), null, null, null);
        return new FileBrowserSessionExecutionCoordinator(runtime, (operation, _) => published.Add(operation));
    }

    private sealed class TrackedResult : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
