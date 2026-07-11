using System.Text;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileInteractionAdvancedInteractionTests : BunitContext
{
    [Fact]
    public async Task TextUnitAutoSave_UsesCumulativeChangedUtf16UnitsAndPublishesAcknowledgedState()
    {
        var profile = TextProfile(
            "unit-text",
            ".units",
            new FileAutoSaveOptions(FileAutoSaveTriggers.TextUnitCount, textUnitCount: 3));
        var composition = TextComposition(profile);
        var saved = new TaskCompletionSource<FileSaveRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var states = new List<FileInteractionState>();
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, Request("draft.units"))
            .Add(component => component.ContentSource, Source("a"))
            .Add(component => component.Composition, composition)
            .Add(component => component.SaveRequested, args => saved.TrySetResult(args.Request))
            .Add(component => component.StateChanged, states.Add));

        await cut.Find("textarea").InputAsync("ab");
        Assert.False(saved.Task.IsCompleted);
        await cut.Find("textarea").InputAsync("abcd");
        var request = await saved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(request.IsAutomatic);
        await using var stream = await request.Content.OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Assert.Equal("abcd", await reader.ReadToEndAsync());
        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Instance.CurrentState.IsDirty);
            Assert.False(cut.Instance.CurrentState.IsSaving);
            Assert.Equal(2, cut.Instance.CurrentState.EditRevision);
            Assert.Contains(states, state =>
                state.EditRevision == 2 && !state.IsDirty && !state.IsSaving);
        });
        Assert.Equal("Saved", SaveStatus(cut));
    }

    [Fact]
    public async Task TextUnitAutoSave_FailurePublishesFinalDirtyErrorState()
    {
        var profile = TextProfile(
            "unit-failure",
            ".failure",
            new FileAutoSaveOptions(FileAutoSaveTriggers.TextUnitCount, textUnitCount: 1));
        var states = new List<FileInteractionState>();
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, Request("draft.failure"))
            .Add(component => component.ContentSource, Source("a"))
            .Add(component => component.Composition, TextComposition(profile))
            .Add(component => component.SaveRequested,
                (FileInteractionSaveRequestedEventArgs _) => throw new IOException("host failed"))
            .Add(component => component.StateChanged, states.Add));

        await cut.Find("textarea").InputAsync("ab");

        cut.WaitForAssertion(() =>
        {
            var state = cut.Instance.CurrentState;
            Assert.True(state.IsDirty);
            Assert.False(state.IsSaving);
            Assert.True(state.HasError);
            Assert.Contains(states, item => item.IsDirty && !item.IsSaving && item.HasError);
        });
        Assert.Equal("Save failed", SaveStatus(cut));
    }

    [Fact]
    public async Task TextUnitAutoSave_ConflictPublishesFinalConflictState()
    {
        var profile = TextProfile(
            "unit-conflict",
            ".conflict",
            new FileAutoSaveOptions(FileAutoSaveTriggers.TextUnitCount, textUnitCount: 1));
        var states = new List<FileInteractionState>();
        var request = Request("draft.conflict");
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, request)
            .Add(component => component.ContentSource, Source("a"))
            .Add(component => component.Composition, TextComposition(profile))
            .Add(component => component.SaveRequested,
                (FileInteractionSaveRequestedEventArgs _) => throw new FileSaveConflictException(
                    request.File,
                    request.ContentRevision,
                    new FileContentRevision("external")))
            .Add(component => component.StateChanged, states.Add));

        await cut.Find("textarea").InputAsync("ab");

        cut.WaitForAssertion(() =>
        {
            var state = cut.Instance.CurrentState;
            Assert.True(state.IsDirty);
            Assert.False(state.IsSaving);
            Assert.True(state.HasConflict);
            Assert.Contains(states, item => item.IsDirty && !item.IsSaving && item.HasConflict);
        });
        Assert.Equal("Save conflict", SaveStatus(cut));
    }

    [Fact]
    public async Task TextUnitAutoSave_EditWhileSaveIsInFlightPublishesNewerRevisionAsDirty()
    {
        var profile = TextProfile(
            "unit-in-flight",
            ".flight",
            new FileAutoSaveOptions(FileAutoSaveTriggers.TextUnitCount, textUnitCount: 2));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var states = new List<FileInteractionState>();
        var saveCalls = 0;
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, Request("draft.flight"))
            .Add(component => component.ContentSource, Source("a"))
            .Add(component => component.Composition, TextComposition(profile))
            .Add(component => component.SaveRequested, async _ =>
            {
                saveCalls++;
                entered.TrySetResult();
                await release.Task;
            })
            .Add(component => component.StateChanged, states.Add));

        await cut.Find("textarea").InputAsync("abc");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cut.Find("textarea").InputAsync("abcd");
        release.TrySetResult();

        cut.WaitForAssertion(() =>
        {
            var state = cut.Instance.CurrentState;
            Assert.Equal(2, state.EditRevision);
            Assert.True(state.IsDirty);
            Assert.False(state.IsSaving);
            Assert.Contains(states, item =>
                item.EditRevision == 2 && item.IsDirty && !item.IsSaving);
        });
        Assert.Equal(1, saveCalls);
        Assert.Equal("Unsaved changes", SaveStatus(cut));
    }

    [Fact]
    public async Task CoalescedManualAndAutomaticSaves_KeepNewestSavingThenFailureState()
    {
        var profile = TextProfile(
            "unit-coalesced",
            ".coalesced",
            new FileAutoSaveOptions(FileAutoSaveTriggers.TextUnitCount, textUnitCount: 2));
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<FileSaveRequest>();
        var states = new List<FileInteractionState>();
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, Request("draft.coalesced"))
            .Add(component => component.ContentSource, Source("a"))
            .Add(component => component.Composition, TextComposition(profile))
            .Add(component => component.SaveRequested, async args =>
            {
                requests.Add(args.Request);
                if (requests.Count == 1)
                {
                    firstEntered.TrySetResult();
                    await releaseFirst.Task;
                    return;
                }

                secondEntered.TrySetResult();
                await releaseSecond.Task;
                throw new IOException("latest save failed");
            })
            .Add(component => component.StateChanged, states.Add));

        await cut.Find("textarea").InputAsync("ab");
        var manualSave = cut.Find("[data-testid='interaction-save']").ClickAsync();
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cut.Find("textarea").InputAsync("abc");
        releaseFirst.TrySetResult();
        await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Instance.CurrentState.IsSaving);
            Assert.Equal("Saving…", SaveStatus(cut));
        });
        releaseSecond.TrySetResult();
        await manualSave;

        cut.WaitForAssertion(() =>
        {
            var state = cut.Instance.CurrentState;
            Assert.Equal(2, state.EditRevision);
            Assert.True(state.IsDirty);
            Assert.False(state.IsSaving);
            Assert.True(state.HasError);
            Assert.Equal("Save failed", SaveStatus(cut));
            Assert.Contains(states, item =>
                item.EditRevision == 2 && item.IsDirty && !item.IsSaving && item.HasError);
        });
        Assert.False(requests[0].IsAutomatic);
        Assert.True(requests[1].IsAutomatic);
    }

    [Fact]
    public async Task TextUnitAutoSave_CompletionFromReplacedRuntimeCannotMutateReplacementState()
    {
        var profile = TextProfile(
            "unit-stale",
            ".stale",
            new FileAutoSaveOptions(FileAutoSaveTriggers.TextUnitCount, textUnitCount: 1));
        var composition = TextComposition(profile);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequest = Request("first.stale");
        var secondRequest = Request("second.stale");
        var replacementStates = new List<FileInteractionState>();
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, firstRequest)
            .Add(component => component.ContentSource, Source("first"))
            .Add(component => component.Composition, composition)
            .Add(component => component.SaveRequested, async _ =>
            {
                entered.TrySetResult();
                await release.Task;
                throw new IOException("stale failure");
            })
            .Add(component => component.StateChanged, replacementStates.Add));

        await cut.Find("textarea").InputAsync("first changed");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var replacement = cut.InvokeAsync(() => cut.Render(parameters => parameters
            .Add(component => component.Request, secondRequest)
            .Add(component => component.ContentSource, Source("second"))
            .Add(component => component.Composition, composition)
            .Add(component => component.SaveRequested, _ => { })
            .Add(component => component.StateChanged, replacementStates.Add)));
        release.TrySetResult();
        await replacement;

        cut.WaitForAssertion(() =>
        {
            var state = cut.Instance.CurrentState;
            Assert.Equal("second.stale", state.FileName);
            Assert.False(state.IsDirty);
            Assert.False(state.IsSaving);
            Assert.False(state.HasError);
        });
        Assert.DoesNotContain(replacementStates, state =>
            state.FileName == "second.stale" && (state.IsDirty || state.IsSaving || state.HasError));
    }

    [Fact]
    public async Task AutoSaveAvailability_AddedAndRemovedDuringSessionIsObservedWithoutFailure()
    {
        var profile = TextProfile(
            "dynamic-save",
            ".dynamic",
            new FileAutoSaveOptions(FileAutoSaveTriggers.TextUnitCount, textUnitCount: 1));
        var composition = TextComposition(profile);
        var request = Request("draft.dynamic");
        var source = Source("a");
        var saveCount = 0;
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, request)
            .Add(component => component.ContentSource, source)
            .Add(component => component.Composition, composition));

        Assert.Equal("Persistence unavailable", SaveStatus(cut));
        await cut.Find("textarea").InputAsync("ab");
        Assert.True(cut.Instance.CurrentState.IsDirty);
        Assert.False(cut.Instance.CurrentState.HasError);
        Assert.Equal("Read-only persistence", SaveStatus(cut));

        cut.Render(parameters => parameters
            .Add(component => component.Request, request)
            .Add(component => component.ContentSource, source)
            .Add(component => component.Composition, composition)
            .Add(component => component.SaveRequested, args =>
            {
                saveCount++;
                saved.TrySetResult();
            }));
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() => Assert.False(cut.Instance.CurrentState.IsDirty));
        Assert.Equal("Saved", SaveStatus(cut));

        cut.Render(parameters => parameters
            .Add(component => component.Request, request)
            .Add(component => component.ContentSource, source)
            .Add(component => component.Composition, composition)
            .Add(
                component => component.SaveRequested,
                default(EventCallback<FileInteractionSaveRequestedEventArgs>)));
        Assert.Equal("Persistence unavailable", SaveStatus(cut));
        await cut.Find("textarea").InputAsync("abc");
        await Task.Delay(50);

        Assert.Equal(1, saveCount);
        Assert.True(cut.Instance.CurrentState.IsDirty);
        Assert.False(cut.Instance.CurrentState.HasError);
        Assert.Equal("Read-only persistence", SaveStatus(cut));
        Assert.Empty(cut.FindAll("[data-testid='interaction-save-error']"));
    }

    [Fact]
    public async Task BinaryEditor_ContentHistoryPreviewMetadataAndSaveUseNeutralChangeEvent()
    {
        var profile = new FileInteractionProfileDescriptor(
            "binary-edit",
            FileInteractionCapabilities.View
                | FileInteractionCapabilities.Edit
                | FileInteractionCapabilities.Preview
                | FileInteractionCapabilities.Save
                | FileInteractionCapabilities.Undo
                | FileInteractionCapabilities.Redo,
            extensions: [".bin"],
            preview: new FilePreviewOptions(true, TimeSpan.Zero, splitByDefault: true),
            history: new FileHistoryOptions(10, 1024));
        var composition = new FileInteractionComponentBuilder()
            .AddProfile(profile)
            .AddHistoryFactory(new BoundedTextHistoryProviderFactory())
            .AddRenderer(new FileInteractionRendererDescriptor(
                "binary-view", profile.Id, FileInteractionMode.View,
                typeof(TestBinaryView), FileInteractionContentKind.Binary))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "binary-edit", profile.Id, FileInteractionMode.Edit,
                typeof(TestBinaryEditor), FileInteractionContentKind.Binary))
            .Build();
        FileSaveRequest? saved = null;
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, new FileInteractionRequest(
                new FileReference("test", "sample.bin"),
                "sample.bin",
                FileInteractionMode.Edit,
                "application/octet-stream"))
            .Add(component => component.ContentSource, Source([1, 2], "application/octet-stream"))
            .Add(component => component.Composition, composition)
            .Add(component => component.SaveRequested, args =>
            {
                saved = args.Request;
                args.SetPersistedRevision(new FileContentRevision("r2"));
            }));

        await cut.Find("[data-testid='binary-apply']").ClickAsync();

        Assert.True(cut.Instance.CurrentState.IsDirty);
        Assert.False(cut.Find("[data-testid='interaction-undo']").HasAttribute("disabled"));
        cut.WaitForAssertion(() => Assert.Contains(
            "9|application/x-edited|binary-v1",
            cut.Find("[data-testid='interaction-preview']").TextContent,
            StringComparison.Ordinal), TimeSpan.FromSeconds(5));

        await cut.Find("[data-testid='interaction-undo']").ClickAsync();
        Assert.Contains("edit:1", cut.Find("[data-testid='binary-editor']").TextContent, StringComparison.Ordinal);
        await cut.Find("[data-testid='interaction-redo']").ClickAsync();
        await cut.Find("[data-testid='interaction-save']").ClickAsync();

        Assert.NotNull(saved);
        Assert.Equal("application/x-edited", saved.MediaType);
        Assert.Equal("binary-v1", saved.EncodingName);
        await using var savedStream = await saved.Content.OpenReadAsync();
        using var buffer = new MemoryStream();
        await savedStream.CopyToAsync(buffer);
        Assert.Equal([9, 8, 7], buffer.ToArray());
        Assert.Equal(
            "r2",
            cut.FindComponent<TestBinaryEditor>().Instance.Context.Request.ContentRevision?.Value);
    }

    [Fact]
    public async Task OversizedTextEdit_IsRejectedVisiblyWithoutDirtyingOrBreakingTheEditor()
    {
        var request = Request("bounded.txt");
        var source = Source("a");
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, request)
            .Add(component => component.ContentSource, source)
            .Add(component => component.MaximumContentBytes, 4));

        await cut.Find("textarea").InputAsync("12345");

        Assert.Contains("4-byte", cut.Find("[data-testid='interaction-edit-error']").TextContent, StringComparison.Ordinal);
        Assert.False(cut.Instance.CurrentState.IsDirty);
        Assert.True(cut.Instance.CurrentState.HasError);
        Assert.Equal("a", cut.Find("textarea").GetAttribute("value"));

        await cut.Find("textarea").InputAsync("ok");

        Assert.Empty(cut.FindAll("[data-testid='interaction-edit-error']"));
        Assert.True(cut.Instance.CurrentState.IsDirty);
    }

    [Theory]
    [InlineData("interaction-conflict-rebase")]
    [InlineData("interaction-conflict-overwrite")]
    public async Task ConflictRetry_StateChangedReplacementNeverSavesTheReplacementFile(string actionTestId)
    {
        var host = Render<ReentrantConflictHost>();
        var cut = host.FindComponent<FileInteraction>();
        await cut.Find("textarea").InputAsync("changed");
        await cut.Find("[data-testid='interaction-save']").ClickAsync();
        host.Instance.ArmReplacement();

        await cut.Find($"[data-testid='{actionTestId}']").ClickAsync();

        host.WaitForAssertion(() => Assert.Equal(
            "second.txt",
            host.FindComponent<FileInteraction>().Instance.CurrentState.FileName));
        Assert.Equal(1, host.Instance.FirstSaveCalls);
        Assert.Equal(0, host.Instance.SecondSaveCalls);
        Assert.False(host.FindComponent<FileInteraction>().Instance.CurrentState.IsDirty);
    }

    [Fact]
    public void ReentrantErrorStateReplacement_DoesNotLeakSupersededLoadFailedEvent()
    {
        var host = Render<ReentrantLoadFailureHost>();

        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='interaction-text-view']")));
        Assert.Equal(0, host.Instance.LoadFailedCalls);
        Assert.Contains("recovered", host.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ControlledParent_ModeChangedArrivesBeforeStateRerenderCanRestoreOldRequestMode()
    {
        var host = Render<ControlledModeHost>();

        await host.Find("[data-testid='interaction-mode-edit']").ClickAsync();

        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='interaction-text-editor']")));
        Assert.Equal(1, host.Instance.ModeChangedCalls);
        Assert.Equal(FileInteractionMode.Edit, host.Instance.AcceptedMode);
    }

    private static FileInteractionProfileDescriptor TextProfile(
        string id,
        string extension,
        FileAutoSaveOptions autoSave)
        => new(
            id,
            FileInteractionCapabilities.View | FileInteractionCapabilities.Edit | FileInteractionCapabilities.Save,
            extensions: [extension],
            autoSave: autoSave);

    private static FileInteractionComponentComposition TextComposition(FileInteractionProfileDescriptor profile)
        => new FileInteractionComponentBuilder()
            .AddProfile(profile)
            .AddRenderer(new FileInteractionRendererDescriptor(
                $"{profile.Id}-view", profile.Id, FileInteractionMode.View,
                typeof(TextFileView), FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                $"{profile.Id}-edit", profile.Id, FileInteractionMode.Edit,
                typeof(TextFileEditor), FileInteractionContentKind.Text))
            .Build();

    private static FileInteractionRequest Request(string fileName)
        => new(
            new FileReference("test", fileName),
            fileName,
            FileInteractionMode.Edit,
            contentRevision: new FileContentRevision("r1"));

    private static IFileContentSource Source(string text)
        => Source(Encoding.UTF8.GetBytes(text), "text/plain");

    private static IFileContentSource Source(byte[] content, string mediaType)
        => new DelegateContentSource((_, _) => ValueTask.FromResult(new FileContentLease(
            new MemoryStream(content, writable: false),
            mediaType,
            content.Length,
            new FileContentRevision("r1"))));

    private static string SaveStatus(IRenderedComponent<FileInteraction> cut)
        => cut.Find("[data-testid='interaction-save-status']").TextContent.Trim();
}

public sealed class TestBinaryEditor : ComponentBase, IFileInteractionRendererComponent
{
    [Parameter]
    public FileInteractionRenderContext Context { get; set; } = default!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "data-testid", "binary-apply");
        builder.AddAttribute(3, "onclick", EventCallback.Factory.Create(
            this,
            () => Context.ContentChanged.InvokeAsync(new FileInteractionContentChange(
                new byte[] { 9, 8, 7 },
                "application/x-edited",
                "binary-v1"))));
        builder.AddContent(4, $"edit:{(Context.Content.IsEmpty ? -1 : Context.Content.Span[0])}");
        builder.CloseElement();
        builder.OpenElement(5, "span");
        builder.AddAttribute(6, "data-testid", "binary-editor");
        builder.AddContent(7, $"edit:{(Context.Content.IsEmpty ? -1 : Context.Content.Span[0])}");
        builder.CloseElement();
    }
}

public sealed class TestBinaryView : ComponentBase, IFileInteractionRendererComponent
{
    [Parameter]
    public FileInteractionRenderContext Context { get; set; } = default!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "data-testid", "binary-view");
        builder.AddContent(
            2,
            $"{(Context.Content.IsEmpty ? -1 : Context.Content.Span[0])}|{Context.MediaType}|{Context.EncodingName}");
        builder.CloseElement();
    }
}

public sealed class ReentrantLoadFailureHost : ComponentBase
{
    private readonly FileInteractionRequest recoveredRequest = new(
        new FileReference("test", "recovered.txt"),
        "recovered.txt");
    private readonly IFileContentSource recoveredSource = new DelegateContentSource((_, _) =>
    {
        var bytes = Encoding.UTF8.GetBytes("recovered");
        return ValueTask.FromResult(new FileContentLease(
            new MemoryStream(bytes, writable: false),
            "text/plain",
            bytes.Length));
    });
    private FileInteractionRequest request = new(
        new FileReference("test", "failed.txt"),
        "failed.txt");
    private IFileContentSource source = new DelegateContentSource((_, _) =>
        ValueTask.FromException<FileContentLease>(new IOException("failed")));

    public int LoadFailedCalls { get; private set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<FileInteraction>(0);
        builder.AddAttribute(1, nameof(FileInteraction.Request), request);
        builder.AddAttribute(2, nameof(FileInteraction.ContentSource), source);
        builder.AddAttribute(3, nameof(FileInteraction.StateChanged), EventCallback.Factory.Create<FileInteractionState>(
            this,
            HandleStateChanged));
        builder.AddAttribute(4, nameof(FileInteraction.LoadFailed), EventCallback.Factory.Create<Exception>(
            this,
            _ => LoadFailedCalls++));
        builder.CloseComponent();
    }

    private void HandleStateChanged(FileInteractionState state)
    {
        if (state.Lifecycle != FileInteractionLifecycleState.Error)
        {
            return;
        }

        request = recoveredRequest;
        source = recoveredSource;
        StateHasChanged();
    }
}

public sealed class ReentrantConflictHost : ComponentBase
{
    private readonly FileInteractionRequest firstRequest = new(
        new FileReference("test", "first.txt"),
        "first.txt",
        FileInteractionMode.Edit,
        contentRevision: new FileContentRevision("r1"));
    private readonly FileInteractionRequest secondRequest = new(
        new FileReference("test", "second.txt"),
        "second.txt",
        FileInteractionMode.Edit,
        contentRevision: new FileContentRevision("r1"));
    private readonly IFileContentSource firstSource = CreateSource("first");
    private readonly IFileContentSource secondSource = CreateSource("second");
    private bool armed;
    private bool showingSecond;

    public int FirstSaveCalls { get; private set; }

    public int SecondSaveCalls { get; private set; }

    public void ArmReplacement() => armed = true;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<FileInteraction>(0);
        builder.AddAttribute(
            1,
            nameof(FileInteraction.Request),
            showingSecond ? secondRequest : firstRequest);
        builder.AddAttribute(
            2,
            nameof(FileInteraction.ContentSource),
            showingSecond ? secondSource : firstSource);
        builder.AddAttribute(
            3,
            nameof(FileInteraction.SaveRequested),
            EventCallback.Factory.Create<FileInteractionSaveRequestedEventArgs>(this, Save));
        builder.AddAttribute(
            4,
            nameof(FileInteraction.StateChanged),
            EventCallback.Factory.Create<FileInteractionState>(this, HandleStateChanged));
        builder.CloseComponent();
    }

    private void Save(FileInteractionSaveRequestedEventArgs args)
    {
        if (args.Request.File == firstRequest.File)
        {
            FirstSaveCalls++;
            throw new FileSaveConflictException(
                firstRequest.File,
                firstRequest.ContentRevision,
                new FileContentRevision("external"));
        }

        SecondSaveCalls++;
    }

    private void HandleStateChanged(FileInteractionState state)
    {
        if (!armed || state.HasConflict || !state.IsDirty)
        {
            return;
        }

        armed = false;
        showingSecond = true;
        StateHasChanged();
    }

    private static IFileContentSource CreateSource(string text)
    {
        var content = Encoding.UTF8.GetBytes(text);
        return new DelegateContentSource((_, _) => ValueTask.FromResult(new FileContentLease(
            new MemoryStream(content, writable: false),
            "text/plain",
            content.Length,
            new FileContentRevision("r1"))));
    }
}

public sealed class ControlledModeHost : ComponentBase
{
    private readonly IFileContentSource source = CreateSource();
    private FileInteractionRequest request = Request(FileInteractionMode.View);

    public int ModeChangedCalls { get; private set; }

    public FileInteractionMode AcceptedMode => request.Mode;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<FileInteraction>(0);
        builder.AddAttribute(1, nameof(FileInteraction.Request), request);
        builder.AddAttribute(2, nameof(FileInteraction.ContentSource), source);
        builder.AddAttribute(3, nameof(FileInteraction.ModeChanged),
            EventCallback.Factory.Create<FileInteractionMode>(this, HandleModeChanged));
        builder.AddAttribute(4, nameof(FileInteraction.StateChanged),
            EventCallback.Factory.Create<FileInteractionState>(this, _ => StateHasChanged()));
        builder.CloseComponent();
    }

    private void HandleModeChanged(FileInteractionMode mode)
    {
        ModeChangedCalls++;
        request = Request(mode);
        StateHasChanged();
    }

    private static FileInteractionRequest Request(FileInteractionMode mode)
        => new(new FileReference("test", "controlled.txt"), "controlled.txt", mode, "text/plain");

    private static IFileContentSource CreateSource()
    {
        var content = Encoding.UTF8.GetBytes("controlled");
        return new DelegateContentSource((_, _) => ValueTask.FromResult(new FileContentLease(
            new MemoryStream(content, writable: false),
            "text/plain",
            content.Length)));
    }
}
