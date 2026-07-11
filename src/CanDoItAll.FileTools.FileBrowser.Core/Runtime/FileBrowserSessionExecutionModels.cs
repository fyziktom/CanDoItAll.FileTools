namespace CanDoItAll.FileTools.FileBrowser;

internal sealed record RetryCommand(
    FileBrowserOperationKind OperationKind,
    Func<CancellationToken, ValueTask> Action);

internal sealed record SessionCheckpoint(
    FileBrowserRuntimeCheckpoint Runtime,
    FileBrowserError? Error,
    RetryCommand? RetryCommand);

internal readonly record struct ExecutionLease(
    CancellationToken LifetimeToken,
    FileBrowserSourceRevision Source)
{
    public CancellationToken SourceToken => Source.Token;

    public long SourceGeneration => Source.Generation;
}

internal static class FileBrowserSessionErrors
{
    public static FileBrowserError ProviderFailure(string message, Exception exception)
        => new(
            FileBrowserErrorCode.ProviderFailure,
            message,
            isRetryable: true,
            technicalDetail: exception.ToString());
}
