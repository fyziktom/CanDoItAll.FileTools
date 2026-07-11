using AngleSharp.Dom;
using FileBrowserComponent = CanDoItAll.FileTools.FileBrowser.Components.FileBrowser;

namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

public sealed class FileBrowserRenderedComponentTests : BunitContext
{
    [Theory]
    [InlineData(FileBrowserViewMode.List, ".ft-file-browser__item-main")]
    [InlineData(FileBrowserViewMode.Cards, ".ft-file-browser__card-main")]
    public void ActivatableNonSelectable_PointerTouchOrNativeSpaceClickInvokesHost(
        FileBrowserViewMode viewMode,
        string selector)
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create(
            "open-only.md",
            capabilities: FileBrowserItemCapabilities.Open);
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file]));
        FileBrowserItemInvokedEventArgs? received = null;
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.InitialViewMode, viewMode)
            .Add(component => component.ItemInvoked, args => received = args));
        IElement primaryButton = cut.Find(selector);

        // Browsers translate pointer/touch and native Space activation of a button into click.
        primaryButton.Click(new MouseEventArgs { Detail = 0 });

        Assert.Equal("BUTTON", primaryButton.TagName);
        Assert.Same(file, received?.Item);
        Assert.Equal(FileBrowserInvocationKind.PrimaryAction, received?.Kind);
        Assert.Empty(session.Selections);
    }

    [Fact]
    public async Task SelectableItem_ClickSelects_EnterAndDoubleClickActivate()
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create();
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file]));
        var invocations = new List<FileBrowserInvocationKind>();
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.ItemInvoked, args => invocations.Add(args.Kind)));
        IElement main = cut.Find(".ft-file-browser__item-main");

        main.Click();
        await main.KeyUpAsync(new KeyboardEventArgs { Key = "Enter" });
        await main.DoubleClickAsync();

        Assert.Equal([(file.Key, false)], session.Selections);
        Assert.Equal(
            [FileBrowserInvocationKind.Keyboard, FileBrowserInvocationKind.PointerDoubleClick],
            invocations);
    }

    [Fact]
    public void NoSources_HidesSourceDependentToolbarAndNavigation()
    {
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create(hasSource: false));
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false));

        Assert.Contains("No sources configured", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".ft-file-browser__toolbar"));
        Assert.Empty(cut.FindAll(".ft-file-browser__location"));
        Assert.Empty(cut.FindAll("input[type=search]"));
        Assert.Empty(cut.FindAll("button[aria-label='Refresh folder']"));
    }

    [Fact]
    public void RootRazor_BindsSearchTextAndRecursiveLabelAsExpressions()
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create();
        var search = new FileBrowserSearchSnapshot(
            "needle",
            FileBrowserSearchScope.LoadedFolder,
            "loaded-folder",
            IsPartial: false,
            ScannedContainers: 1,
            ScannedItems: 1,
            NextContinuationToken: null,
            TotalCount: 1);
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file], search: search));
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false)
            .Add(component => component.IncludeDescendantsLabel, "Include project descendants"));

        Assert.Equal("needle", cut.Find("input[type=search]").GetAttribute("value"));
        Assert.Equal(
            "Include project descendants",
            cut.Find(".ft-file-browser__recursive-toggle span").TextContent.Trim());
        Assert.DoesNotContain(">searchText<", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">IncludeDescendantsLabel<", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameKeyRevisionChange_CancelsAndRejectsInFlightActions()
    {
        FileBrowserItem original = TestFileBrowserItemFactory.Create();
        FileBrowserItem changed = RenderedFileBrowserSnapshotFactory.Recreate(
            original,
            "changed.md",
            FileBrowserItemCapabilities.Select);
        FileBrowserSourceDescriptor source = RenderedFileBrowserSnapshotFactory.CreateSource();
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([original], currentSource: source));
        var action = new FileBrowserActionDescriptor(FileBrowserActionIds.Open, "Open", "open");
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.GetActionsHandler = async (_, _) =>
        {
            loadStarted.SetResult();
            await releaseLoad.Task;
            return [action];
        };
        IRenderedComponent<FileBrowserItemActions> cut = Render<FileBrowserItemActions>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.Item, original)
            .Add(component => component.Source, source)
            .Add(component => component.SnapshotRevision, 7L));

        Task pendingLoad = cut.Find(".ft-file-browser__action-menu-button").ClickAsync();
        await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cut.Render(parameters => parameters
            .Add(component => component.Item, changed)
            .Add(component => component.SnapshotRevision, 8L));
        releaseLoad.SetResult();
        await pendingLoad;

        Assert.Empty(cut.FindAll(".ft-file-browser__action-item"));
        Assert.Contains("No actions available", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadedActions_BecomeDisabledWithBusySnapshot()
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create();
        FileBrowserSourceDescriptor source = RenderedFileBrowserSnapshotFactory.CreateSource();
        var action = new FileBrowserActionDescriptor(FileBrowserActionIds.Open, "Open", "open");
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file], currentSource: source))
        {
            GetActionsHandler = (_, _) =>
                ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>([action])
        };
        IRenderedComponent<FileBrowserItemActions> cut = Render<FileBrowserItemActions>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.Item, file)
            .Add(component => component.Source, source)
            .Add(component => component.SnapshotRevision, 3L));
        await cut.Find(".ft-file-browser__action-menu-button").ClickAsync();

        cut.Render(parameters => parameters.Add(component => component.Disabled, true));

        Assert.True(cut.Find(".ft-file-browser__action-item").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ActionPopover_UsesNativeTopLayerWithSimpleButtonGroupSemantics()
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create();
        FileBrowserSourceDescriptor source = RenderedFileBrowserSnapshotFactory.CreateSource();
        var action = new FileBrowserActionDescriptor(FileBrowserActionIds.Open, "Open", "open");
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file], currentSource: source))
        {
            GetActionsHandler = (_, _) =>
                ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>([action])
        };
        IRenderedComponent<FileBrowserItemActions> cut = Render<FileBrowserItemActions>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.Item, file)
            .Add(component => component.Source, source)
            .Add(component => component.SnapshotRevision, 2L));
        IElement trigger = cut.Find(".ft-file-browser__action-menu-button");
        await trigger.ClickAsync();

        IElement popover = cut.Find("[popover=auto]");
        Assert.True(trigger.HasAttribute("popovertarget"));
        Assert.True(trigger.HasAttribute("aria-controls"));
        Assert.False(trigger.HasAttribute("aria-haspopup"));
        Assert.Equal("group", popover.GetAttribute("role"));
        Assert.Empty(cut.FindAll("[role=menuitem]"));
        Assert.Equal("BUTTON", cut.Find(".ft-file-browser__action-item").TagName);
    }

    [Fact]
    public void DuplicateWarningIdentity_RendersEveryWarningWithoutKeyCollision()
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create();
        IReadOnlyList<FileBrowserPageWarning> warnings =
        [
            new("changing-entry", "First warning", file.Key),
            new("changing-entry", "Second warning", file.Key)
        ];
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create([file], warnings: warnings));
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false));

        IReadOnlyList<IElement> renderedWarnings = cut.FindAll(".ft-file-browser__warnings li");
        Assert.Equal(2, renderedWarnings.Count);
        Assert.Equal(["First warning", "Second warning"], renderedWarnings.Select(item => item.TextContent));
    }

    [Fact]
    public void MultiSourceNavigation_DispatchesSelectedSourceThroughRenderedControl()
    {
        FileBrowserSourceDescriptor first = RenderedFileBrowserSnapshotFactory.CreateSource(
            "first",
            "First source");
        FileBrowserSourceDescriptor second = RenderedFileBrowserSnapshotFactory.CreateSource(
            "second",
            "Second source");
        var session = new RenderedFileBrowserTestSession(
            RenderedFileBrowserSnapshotFactory.Create(
                sources: [first, second],
                currentSource: first));
        IRenderedComponent<FileBrowserComponent> cut = Render<FileBrowserComponent>(parameters => parameters
            .Add(component => component.Session, session)
            .Add(component => component.InitializeOnFirstRender, false));
        IElement secondButton = cut.FindAll(".ft-file-browser__source-button")
            .Single(button => button.TextContent.Contains("Second source", StringComparison.Ordinal));

        secondButton.Click();

        Assert.Equal([second.Id], session.SourceChanges);
    }
}
