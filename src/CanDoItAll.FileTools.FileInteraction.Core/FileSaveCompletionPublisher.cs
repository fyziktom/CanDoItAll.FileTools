namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>Isolates post-transition observers from the persistence runner.</summary>
internal sealed class FileSaveCompletionPublisher
{
    public event EventHandler<FileSaveCompletedEventArgs>? Completed;

    public void Publish(
        object sender,
        FileSaveOperationResult result,
        FileEditSessionState state)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(state);
        var handlers = Completed;
        if (handlers is null)
        {
            return;
        }

        var args = new FileSaveCompletedEventArgs(result, state);
        foreach (EventHandler<FileSaveCompletedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(sender, args);
            }
            catch
            {
                // Completion observers must never change an already-determined persistence result.
            }
        }
    }
}
