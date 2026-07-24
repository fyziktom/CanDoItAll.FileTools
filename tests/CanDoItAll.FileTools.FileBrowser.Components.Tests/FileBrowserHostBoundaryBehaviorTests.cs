using AngleSharp.Dom;
using FileBrowserComponent = CanDoItAll.FileTools.FileBrowser.Components.FileBrowser;

namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

public sealed class FileBrowserHostBoundaryBehaviorTests : FileToolsBunitContext
{
    [Fact]
    public async Task FileDoubleClick_AwaitsHostItemInvokedCallback()
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create();
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file]));
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        FileBrowserItemInvokedEventArgs? received = null;
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.ItemInvoked, async args =>
            {
                received = args;
                callbackStarted.SetResult();
                await releaseCallback.Task;
            }));

        Task activation = cut.Find(".ft-file-browser__item-main").DoubleClickAsync();
        await callbackStarted.Task.WaitAsync(AsyncOperationTimeout);

        Assert.False(activation.IsCompleted);
        releaseCallback.SetResult();
        await activation;
        Assert.Same(file, received?.Item);
        Assert.Equal(FileBrowserInvocationKind.PointerDoubleClick, received?.Kind);
    }

    [Fact]
    public async Task FolderEnter_NavigatesSessionWithoutInvokingHostViewer()
    {
        FileBrowserItem folder = TestFileBrowserItemFactory.Create(
            "folder",
            FileBrowserItemKind.Container,
            FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([folder]));
        int hostInvocations = 0;
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.ItemInvoked, _ => hostInvocations++));

        await cut.Find(".ft-file-browser__item-main").KeyUpAsync(
            new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal([folder.Key], session.Navigations);
        Assert.Equal(0, hostInvocations);
    }

    [Fact]
    public async Task ActionButton_AwaitsHostAndNeverExecutesSessionAction()
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create();
        var action = new FileBrowserActionDescriptor(FileBrowserActionIds.Open, "Open", "open");
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file]));
        session.GetActionsHandler = (_, _) =>
            ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>([action]);
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.ActionRequested, async _ =>
            {
                callbackStarted.SetResult();
                await releaseCallback.Task;
            }));
        await cut.Find(".ft-file-browser__action-menu-button").ClickAsync();
        IElement actionButton = cut.WaitForElement(".ft-file-browser__action-item");

        Task forwarding = actionButton.ClickAsync();
        await callbackStarted.Task.WaitAsync(AsyncOperationTimeout);

        Assert.False(forwarding.IsCompleted);
        Assert.Equal(0, session.ExecuteActionCalls);
        releaseCallback.SetResult();
        await forwarding;
        Assert.Equal(0, session.ExecuteActionCalls);
    }

    [Fact]
    public async Task DetachedCallbacks_AreRejectedAfterSameKeySessionReplacement()
    {
        FileBrowserItem oldItem = TestFileBrowserItemFactory.Create("same-key.md");
        FileBrowserItem replacementItem = RenderedFileBrowserSnapshotFactory.Recreate(
            oldItem,
            "replacement-name.md");
        var firstSession = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([oldItem], revision: 11));
        var replacementSession = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([replacementItem], revision: 11));
        int itemInvocations = 0;
        int actionRequests = 0;
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, firstSession)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.ItemInvoked, _ => itemInvocations++)
            .Add(component => component.ActionRequested, _ => actionRequests++));
        IRenderedComponent<FileBrowserListView> oldList = cut.FindComponent<FileBrowserListView>();
        EventCallback<FileBrowserItem> staleSelect = oldList.Instance.SelectRequested;
        EventCallback<FileBrowserItemInvokedEventArgs> staleActivate = oldList.Instance.ActivateRequested;
        EventCallback<FileBrowserItemActionEventArgs> staleAction = oldList.Instance.ActionRequested;
        var open = new FileBrowserActionDescriptor(FileBrowserActionIds.Open, "Open", "open");

        cut.Render(parameters => parameters.Add(component => component.Session, replacementSession));
        await cut.InvokeAsync(() => staleSelect.InvokeAsync(oldItem));
        await cut.InvokeAsync(() => staleActivate.InvokeAsync(new FileBrowserItemInvokedEventArgs(
            oldItem,
            FileBrowserInvocationKind.PointerDoubleClick)));
        await cut.InvokeAsync(() => staleAction.InvokeAsync(new FileBrowserItemActionEventArgs(oldItem, open)));

        Assert.Empty(replacementSession.Selections);
        Assert.Empty(replacementSession.Navigations);
        Assert.Equal(0, itemInvocations);
        Assert.Equal(0, actionRequests);

        await cut.Find(".ft-file-browser__item-main").DoubleClickAsync();
        Assert.Equal(1, itemInvocations);
    }

    [Fact]
    public async Task DetachedCallbacks_AreRejectedAfterSameSessionSameKeySnapshotReplacement()
    {
        FileBrowserItem oldItem = TestFileBrowserItemFactory.Create("same-key.md");
        FileBrowserItem replacementItem = RenderedFileBrowserSnapshotFactory.Recreate(
            oldItem,
            "same-session-replacement.md");
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([oldItem], revision: 20));
        int itemInvocations = 0;
        int actionRequests = 0;
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.ItemInvoked, _ => itemInvocations++)
            .Add(component => component.ActionRequested, _ => actionRequests++));
        IRenderedComponent<FileBrowserListView> oldList = cut.FindComponent<FileBrowserListView>();
        EventCallback<FileBrowserItem> staleSelect = oldList.Instance.SelectRequested;
        EventCallback<FileBrowserItemInvokedEventArgs> staleActivate = oldList.Instance.ActivateRequested;
        EventCallback<FileBrowserItemActionEventArgs> staleAction = oldList.Instance.ActionRequested;
        var open = new FileBrowserActionDescriptor(FileBrowserActionIds.Open, "Open", "open");

        await cut.InvokeAsync(() => session.Publish(
            RenderedFileBrowserSnapshotFactory.Create([replacementItem], revision: 21)));
        cut.WaitForAssertion(() => Assert.Contains(
            "same-session-replacement.md",
            cut.Markup,
            StringComparison.Ordinal));
        await cut.InvokeAsync(() => staleSelect.InvokeAsync(oldItem));
        await cut.InvokeAsync(() => staleActivate.InvokeAsync(new FileBrowserItemInvokedEventArgs(
            oldItem,
            FileBrowserInvocationKind.PointerDoubleClick)));
        await cut.InvokeAsync(() => staleAction.InvokeAsync(new FileBrowserItemActionEventArgs(oldItem, open)));

        Assert.Empty(session.Selections);
        Assert.Equal(0, itemInvocations);
        Assert.Equal(0, actionRequests);

        IRenderedComponent<FileBrowserListView> currentList = cut.FindComponent<FileBrowserListView>();
        await cut.InvokeAsync(() => currentList.Instance.SelectRequested.InvokeAsync(replacementItem));
        await cut.InvokeAsync(() => currentList.Instance.ActivateRequested.InvokeAsync(
            new FileBrowserItemInvokedEventArgs(
                replacementItem,
                FileBrowserInvocationKind.PointerDoubleClick)));
        await cut.InvokeAsync(() => currentList.Instance.ActionRequested.InvokeAsync(
            new FileBrowserItemActionEventArgs(replacementItem, open)));

        Assert.Equal([(replacementItem.Key, false)], session.Selections);
        Assert.Equal(1, itemInvocations);
        Assert.Equal(1, actionRequests);
    }
}
