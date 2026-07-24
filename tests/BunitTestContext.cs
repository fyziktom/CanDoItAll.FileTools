public abstract class FileToolsBunitContext : Bunit.BunitContext
{
    protected static TimeSpan AsyncOperationTimeout { get; } = TimeSpan.FromSeconds(5);

    protected FileToolsBunitContext()
    {
        DefaultWaitTimeout = AsyncOperationTimeout;
    }
}
