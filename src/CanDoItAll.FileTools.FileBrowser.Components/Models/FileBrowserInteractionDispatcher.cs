using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileBrowser.Components;

/// <summary>
/// Captures render-time session/snapshot identity and rejects callbacks from superseded render
/// trees before they can reach a session or host effect boundary.
/// </summary>
internal sealed class FileBrowserInteractionDispatcher
{
    private readonly FileBrowserInteractionGuard guard = new();

    public void ChangeSession() => guard.ChangeSession();

    public void AcceptSnapshot() => guard.AcceptSnapshot();

    public FileBrowserInteractionStamp Capture() => guard.Capture();

    public bool IsCurrentSession(long expectedSessionVersion)
        => guard.IsCurrentSession(expectedSessionVersion);

    public EventCallback<FileBrowserItem> CreateSelectCallback(
        object receiver,
        IFileBrowserSession session,
        FileBrowserSnapshot snapshot,
        bool toggle)
    {
        FileBrowserInteractionStamp stamp = guard.Capture();
        return EventCallback.Factory.Create<FileBrowserItem>(
            receiver,
            item => Select(item, toggle, session, snapshot, stamp));
    }

    public EventCallback<FileBrowserItemInvokedEventArgs> CreateActivateCallback(
        object receiver,
        IFileBrowserSession session,
        FileBrowserSnapshot snapshot,
        CancellationToken cancellationToken,
        EventCallback<FileBrowserItemInvokedEventArgs> hostCallback)
    {
        FileBrowserInteractionStamp stamp = guard.Capture();
        return EventCallback.Factory.Create<FileBrowserItemInvokedEventArgs>(
            receiver,
            args => ActivateAsync(
                args,
                session,
                snapshot,
                stamp,
                cancellationToken,
                hostCallback));
    }

    public EventCallback<FileBrowserItemActionEventArgs> CreateActionCallback(
        object receiver,
        IFileBrowserSession session,
        FileBrowserSnapshot snapshot,
        EventCallback<FileBrowserItemActionEventArgs> hostCallback)
    {
        FileBrowserInteractionStamp stamp = guard.Capture();
        return EventCallback.Factory.Create<FileBrowserItemActionEventArgs>(
            receiver,
            args => ForwardActionAsync(args, session, snapshot, stamp, hostCallback));
    }

    private void Select(
        FileBrowserItem item,
        bool toggle,
        IFileBrowserSession session,
        FileBrowserSnapshot snapshot,
        FileBrowserInteractionStamp stamp)
    {
        if (TryResolveCurrentItem(item, snapshot, stamp, out FileBrowserItem current)
            && FileBrowserInteractionPolicy.CanSelect(current))
        {
            session.Select(current.Key, toggle);
        }
    }

    private async Task ActivateAsync(
        FileBrowserItemInvokedEventArgs args,
        IFileBrowserSession session,
        FileBrowserSnapshot snapshot,
        FileBrowserInteractionStamp stamp,
        CancellationToken cancellationToken,
        EventCallback<FileBrowserItemInvokedEventArgs> hostCallback)
    {
        if (!TryResolveCurrentItem(args.Item, snapshot, stamp, out FileBrowserItem current)
            || !FileBrowserInteractionPolicy.CanActivate(current))
        {
            return;
        }

        if (FileBrowserInteractionPolicy.NavigatesInternally(current))
        {
            try
            {
                await session.NavigateAsync(current.Key, cancellationToken);
            }
            catch (OperationCanceledException) when (!guard.IsCurrent(stamp))
            {
                // Session replacement or component disposal superseded navigation.
            }

            return;
        }

        await hostCallback.InvokeAsync(args with { Item = current });
    }

    private async Task ForwardActionAsync(
        FileBrowserItemActionEventArgs args,
        IFileBrowserSession session,
        FileBrowserSnapshot snapshot,
        FileBrowserInteractionStamp stamp,
        EventCallback<FileBrowserItemActionEventArgs> hostCallback)
    {
        if (!TryResolveCurrentItem(args.Item, snapshot, stamp, out FileBrowserItem current)
            || !FileBrowserInteractionPolicy.IsActionSupported(
                current,
                snapshot.CurrentSource,
                args.ActionId))
        {
            return;
        }

        await hostCallback.InvokeAsync(args with { Item = current });
    }

    private bool TryResolveCurrentItem(
        FileBrowserItem candidate,
        FileBrowserSnapshot snapshot,
        FileBrowserInteractionStamp stamp,
        out FileBrowserItem current)
    {
        current = (IsReplacing(snapshot.Operation) || !guard.IsCurrent(stamp)
            ? null
            : snapshot.Items.FirstOrDefault(item => item.Key == candidate.Key))!;
        return current is not null
            && (ReferenceEquals(current, candidate) || current == candidate);
    }

    private static bool IsReplacing(FileBrowserOperationKind operation)
        => operation is FileBrowserOperationKind.Initializing
            or FileBrowserOperationKind.LoadingFolder
            or FileBrowserOperationKind.Refreshing
            or FileBrowserOperationKind.Searching;
}
