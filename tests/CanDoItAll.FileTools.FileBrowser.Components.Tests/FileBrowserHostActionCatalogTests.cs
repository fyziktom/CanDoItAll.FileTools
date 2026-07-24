using AngleSharp.Dom;
using FileBrowserComponent = CanDoItAll.FileTools.FileBrowser.Components.FileBrowser;

namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

public sealed class FileBrowserHostActionCatalogTests : FileToolsBunitContext
{
    [Theory]
    [InlineData(FileBrowserViewMode.List)]
    [InlineData(FileBrowserViewMode.Cards)]
    public async Task HostAction_IsPresentedAndForwardedWithoutChangingItemCapabilities(
        FileBrowserViewMode viewMode)
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create(
            "report.xlsx",
            capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Preview);
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file], revision: 17));
        var open = new FileBrowserActionDescriptor(
            FileBrowserActionIds.Open,
            "Open in preferred app",
            "open");
        var catalog = new FixedHostActionCatalog([open]);
        FileBrowserItemActionEventArgs? received = null;
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.InitialViewMode, viewMode)
            .Add(component => component.HostActionCatalog, catalog)
            .Add(component => component.ActionRequested, args => received = args));

        await cut.Find(".ft-file-browser__action-menu-button").ClickAsync();
        IElement action = cut.WaitForElement(".ft-file-browser__action-item");
        await action.ClickAsync();

        FileBrowserHostActionContext context = Assert.Single(catalog.Contexts);
        Assert.Same(file, context.Item);
        Assert.Equal(17, context.SnapshotRevision);
        Assert.Same(file, received?.Item);
        Assert.Same(open, received?.Action);
        Assert.Equal(FileBrowserActionOrigin.Host, received?.Origin);
        Assert.False(file.Supports(FileBrowserItemCapabilities.Open));
        Assert.False(file.Supports(FileBrowserItemCapabilities.CustomActions));
        Assert.Equal(0, session.ExecuteActionCalls);
    }

    [Fact]
    public async Task DuplicateActionIdAcrossSessionAndHost_IsRejectedBeforePresentation()
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create();
        var open = new FileBrowserActionDescriptor(FileBrowserActionIds.Open, "Open", "open");
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file]));
        session.GetActionsHandler = (_, _) =>
            ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>([open]);
        var catalog = new FixedHostActionCatalog([
            new FileBrowserActionDescriptor(FileBrowserActionIds.Open, "Open externally", "open")
        ]);
        int requests = 0;
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.HostActionCatalog, catalog)
            .Add(component => component.ActionRequested, _ => requests++));

        await cut.Find(".ft-file-browser__action-menu-button").ClickAsync();

        IElement error = cut.WaitForElement(".ft-file-browser__action-menu-status[role='alert']");
        Assert.Equal("Actions could not be loaded.", error.TextContent.Trim());
        Assert.Empty(cut.FindAll(".ft-file-browser__action-item"));
        Assert.Equal(0, requests);
    }

    [Fact]
    public void DuplicateActionIdWithinHostCatalog_IsRejected()
    {
        var first = new FileBrowserActionDescriptor("host.open", "First", "open");
        var second = new FileBrowserActionDescriptor("host.open", "Second", "open");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            FileBrowserPresentedActionCatalog.Merge([], [first, second]));

        Assert.Contains("host.open", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FixedHostActionCatalog(
        IReadOnlyList<FileBrowserActionDescriptor> actions) : IFileBrowserHostActionCatalog
    {
        public List<FileBrowserHostActionContext> Contexts { get; } = [];

        public ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
            FileBrowserHostActionContext context,
            CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return ValueTask.FromResult(actions);
        }
    }
}
