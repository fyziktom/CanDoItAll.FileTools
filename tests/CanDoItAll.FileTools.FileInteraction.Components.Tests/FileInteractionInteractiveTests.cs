using System.Text;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileInteractionInteractiveTests : FileToolsBunitContext
{
    [Fact]
    public async Task EditUndoRedoPreviewAndManualSave_DriveRenderedShellAndAwaitHost()
    {
        var states = new List<FileInteractionState>();
        var hostEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? savedText = null;
        bool? wasAutomatic = null;
        var cut = RenderEditor("notes.txt", "alpha", async args =>
        {
            await using var stream = await args.Request.Content.OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            savedText = await reader.ReadToEndAsync();
            wasAutomatic = args.Request.IsAutomatic;
            hostEntered.SetResult();
            await releaseHost.Task;
            args.SetPersistedRevision(new FileContentRevision("r2"));
        }, states.Add);

        await cut.Find("textarea").InputAsync("beta");
        Assert.Equal("Unsaved changes", Status(cut));
        Assert.False(cut.Find("[data-testid='interaction-undo']").HasAttribute("disabled"));

        await cut.Find("[data-testid='interaction-undo']").ClickAsync();
        Assert.Equal("alpha", EditorValue(cut));
        await cut.Find("[data-testid='interaction-redo']").ClickAsync();
        Assert.Equal("beta", EditorValue(cut));

        await cut.Find("[data-testid='interaction-preview-toggle']").ClickAsync();
        cut.WaitForAssertion(
            () => Assert.Contains(
                "beta",
                cut.Find("[data-testid='interaction-preview']").TextContent,
                StringComparison.Ordinal),
            AsyncOperationTimeout);

        Task saving = cut.Find("[data-testid='interaction-save']").ClickAsync();
        await hostEntered.Task.WaitAsync(AsyncOperationTimeout);
        Assert.False(saving.IsCompleted);
        cut.WaitForAssertion(() => Assert.Equal("Saving…", Status(cut)));
        releaseHost.SetResult();
        await saving;

        cut.WaitForAssertion(() => Assert.Equal("Saved", Status(cut)));
        Assert.Equal("beta", savedText);
        Assert.False(wasAutomatic);
        Assert.True(cut.Find("[data-testid='interaction-save']").HasAttribute("disabled"));
        Assert.Contains(states, state => state.IsDirty && !state.IsSaving);
        Assert.Contains(states, state => state.IsDirty && state.IsSaving);
        Assert.False(cut.Instance.CurrentState.IsDirty);
        Assert.False(cut.Instance.CurrentState.IsSaving);
    }

    [Fact]
    public async Task FailedManualSave_LeavesRenderedEditorDirtyAndRetryable()
    {
        var cut = RenderEditor(
            "notes.txt",
            "alpha",
            (FileInteractionSaveRequestedEventArgs _) => throw new IOException("host failed"));
        await cut.Find("textarea").InputAsync("beta");

        await cut.Find("[data-testid='interaction-save']").ClickAsync();

        Assert.Equal("Save failed", Status(cut));
        Assert.Contains(
            "Your changes remain available",
            cut.Find("[data-testid='interaction-save-error']").TextContent,
            StringComparison.Ordinal);
        Assert.Equal("beta", EditorValue(cut));
        Assert.False(cut.Find("[data-testid='interaction-save']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ChangeCountAutoSave_ReportsAutomaticSavingAndCompletionInRenderedShell()
    {
        var profile = new FileInteractionProfileDescriptor(
            "auto-text",
            FileInteractionCapabilities.View | FileInteractionCapabilities.Edit | FileInteractionCapabilities.Save,
            extensions: [".auto"],
            autoSave: new FileAutoSaveOptions(FileAutoSaveTriggers.ChangeCount, changeCount: 1));
        var composition = new FileInteractionComponentBuilder()
            .AddProfile(profile)
            .AddRenderer(new FileInteractionRendererDescriptor(
                "auto-view", profile.Id, FileInteractionMode.View,
                typeof(TextFileView), FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "auto-edit", profile.Id, FileInteractionMode.Edit,
                typeof(TextFileEditor), FileInteractionContentKind.Text))
            .Build();
        var entered = new TaskCompletionSource<FileSaveRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, Request("draft.auto"))
            .Add(component => component.ContentSource, Source("alpha"))
            .Add(component => component.Composition, composition)
            .Add(component => component.SaveRequested, async args =>
            {
                entered.TrySetResult(args.Request);
                await release.Task;
            }));

        await cut.Find("textarea").InputAsync("beta");
        var request = await entered.Task.WaitAsync(AsyncOperationTimeout);

        Assert.True(request.IsAutomatic);
        cut.WaitForAssertion(() => Assert.Equal("Saving…", Status(cut)));
        release.SetResult();
        cut.WaitForAssertion(() => Assert.Equal("Saved", Status(cut)), AsyncOperationTimeout);
    }

    [Fact]
    public async Task EditProfileWithoutSave_NeverEnablesOrInvokesHostPersistence()
    {
        var profile = new FileInteractionProfileDescriptor(
            "read-only-edit",
            FileInteractionCapabilities.View | FileInteractionCapabilities.Edit,
            extensions: [".readonly"]);
        var composition = TextComposition(profile);
        var saveCalls = 0;
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, Request("draft.readonly"))
            .Add(component => component.ContentSource, Source("alpha"))
            .Add(component => component.Composition, composition)
            .Add(component => component.SaveRequested, _ => saveCalls++));

        await cut.Find("textarea").InputAsync("beta");
        await cut.Find("[data-testid='interaction-save']").ClickAsync();

        Assert.Equal("Read-only persistence", Status(cut));
        Assert.True(cut.Find("[data-testid='interaction-save']").HasAttribute("disabled"));
        Assert.Equal(0, saveCalls);
        Assert.True(cut.Instance.CurrentState.IsDirty);
    }

    [Fact]
    public async Task HistoryStateWithoutUndoRedoCapabilities_NeverEnablesThoseOperations()
    {
        var profile = new FileInteractionProfileDescriptor(
            "edit-with-masked-history",
            FileInteractionCapabilities.View | FileInteractionCapabilities.Edit,
            extensions: [".masked"]);
        var history = new AlwaysAvailableHistoryProvider();
        var composition = new FileInteractionComponentBuilder()
            .AddProfile(profile)
            .AddHistoryFactory(new FixedHistoryFactory(history))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "masked-view", profile.Id, FileInteractionMode.View,
                typeof(TextFileView), FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "masked-edit", profile.Id, FileInteractionMode.Edit,
                typeof(TextFileEditor), FileInteractionContentKind.Text))
            .Build();
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, Request("draft.masked"))
            .Add(component => component.ContentSource, Source("alpha"))
            .Add(component => component.Composition, composition));

        await cut.Find("textarea").InputAsync("beta");
        await cut.Find("[data-testid='interaction-undo']").ClickAsync();
        await cut.Find("[data-testid='interaction-redo']").ClickAsync();

        Assert.True(cut.Find("[data-testid='interaction-undo']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='interaction-redo']").HasAttribute("disabled"));
        Assert.False(cut.Instance.CurrentState.CanUndo);
        Assert.False(cut.Instance.CurrentState.CanRedo);
        Assert.Equal(0, history.UndoCalls);
        Assert.Equal(0, history.RedoCalls);
    }

    [Fact]
    public async Task SaveConflict_RebaseActionRetriesAgainstCurrentRevisionAndCompletes()
    {
        var file = new FileReference("test", "notes.txt");
        var requests = new List<FileSaveRequest>();
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, new FileInteractionRequest(
                file,
                "notes.txt",
                FileInteractionMode.Edit,
                contentRevision: new FileContentRevision("r1")))
            .Add(component => component.ContentSource, Source("alpha"))
            .Add(component => component.SaveRequested, args =>
            {
                requests.Add(args.Request);
                if (requests.Count == 1)
                {
                    throw new FileSaveConflictException(
                        file,
                        new FileContentRevision("r1"),
                        new FileContentRevision("r2"));
                }

                args.SetPersistedRevision(new FileContentRevision("r3"));
            }));
        await cut.Find("textarea").InputAsync("beta");

        await cut.Find("[data-testid='interaction-save']").ClickAsync();

        Assert.Equal("Save conflict", Status(cut));
        Assert.True(cut.Instance.CurrentState.HasConflict);
        Assert.True(cut.Instance.CurrentState.IsDirty);
        Assert.NotNull(cut.Find("[data-testid='interaction-conflict-rebase']"));
        Assert.NotNull(cut.Find("[data-testid='interaction-conflict-overwrite']"));
        await cut.Find("[data-testid='interaction-conflict-rebase']").ClickAsync();

        Assert.Equal(2, requests.Count);
        Assert.Equal("r2", requests[1].ExpectedRevision?.Value);
        Assert.Equal("Saved", Status(cut));
        Assert.False(cut.Instance.CurrentState.HasConflict);
        Assert.False(cut.Instance.CurrentState.IsDirty);
        Assert.Equal(
            "r3",
            cut.FindComponent<TextFileEditor>().Instance.Context.Request.ContentRevision?.Value);
    }

    [Fact]
    public async Task SaveConflict_OverwriteActionRetriesWithoutExpectedRevisionAndCompletes()
    {
        var file = new FileReference("test", "notes.txt");
        var requests = new List<FileSaveRequest>();
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, new FileInteractionRequest(
                file,
                "notes.txt",
                FileInteractionMode.Edit,
                contentRevision: new FileContentRevision("r1")))
            .Add(component => component.ContentSource, Source("alpha"))
            .Add(component => component.SaveRequested, args =>
            {
                requests.Add(args.Request);
                if (requests.Count == 1)
                {
                    throw new FileSaveConflictException(
                        file,
                        new FileContentRevision("r1"),
                        new FileContentRevision("r2"));
                }
            }));
        await cut.Find("textarea").InputAsync("beta");
        await cut.Find("[data-testid='interaction-save']").ClickAsync();

        var overwrite = cut.Find("[data-testid='interaction-conflict-overwrite']");
        Assert.Contains("without revision", overwrite.TextContent, StringComparison.Ordinal);
        await overwrite.ClickAsync();

        Assert.Equal(2, requests.Count);
        Assert.Null(requests[1].ExpectedRevision);
        Assert.Equal("Saved", Status(cut));
        Assert.False(cut.Instance.CurrentState.HasConflict);
        Assert.False(cut.Instance.CurrentState.IsDirty);
        Assert.Null(cut.FindComponent<TextFileEditor>().Instance.Context.Request.ContentRevision);
    }

    [Fact]
    public async Task DetachedEditorCallback_AfterFileReplacementCannotEditNewFile()
    {
        var states = new List<FileInteractionState>();
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, Request("first.txt"))
            .Add(component => component.ContentSource, Source("first"))
            .Add(component => component.StateChanged, states.Add));
        EventCallback<string> staleTextChanged = cut
            .FindComponent<TextFileEditor>()
            .Instance.Context.TextChanged;

        cut.Render(parameters => parameters
            .Add(component => component.Request, Request("second.txt"))
            .Add(component => component.ContentSource, Source("second"))
            .Add(component => component.StateChanged, states.Add));
        states.Clear();
        await cut.InvokeAsync(() => staleTextChanged.InvokeAsync("stale"));

        Assert.Equal("second", EditorValue(cut));
        Assert.Equal("Persistence unavailable", Status(cut));
        Assert.Empty(states);
        Assert.Equal("second.txt", cut.Instance.CurrentState.FileName);
        Assert.False(cut.Instance.CurrentState.IsDirty);
    }

    [Fact]
    public async Task SupersededBlockedHistoryCreation_CannotReplaceOrDirtyNewFile()
    {
        var states = new List<FileInteractionState>();
        var factory = new BlockingHistoryFactory();
        var profile = new FileInteractionProfileDescriptor(
            "blocking-text",
            FileInteractionCapabilities.View
                | FileInteractionCapabilities.Edit
                | FileInteractionCapabilities.Undo
                | FileInteractionCapabilities.Redo,
            extensions: [".block"],
            history: new FileHistoryOptions(10, 1024));
        var composition = new FileInteractionComponentBuilder()
            .AddProfile(profile)
            .AddHistoryFactory(factory)
            .AddRenderer(new FileInteractionRendererDescriptor(
                "blocking-view", profile.Id, FileInteractionMode.View,
                typeof(TextFileView), FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "blocking-edit", profile.Id, FileInteractionMode.Edit,
                typeof(TextFileEditor), FileInteractionContentKind.Text))
            .Build();
        var cut = Render<FileInteraction>(parameters => parameters
            .Add(component => component.Request, Request("first.block", FileInteractionMode.View))
            .Add(component => component.ContentSource, Source("first"))
            .Add(component => component.Composition, composition)
            .Add(component => component.StateChanged, states.Add));

        Task openingEditor = cut.Find("[data-testid='interaction-mode-edit']").ClickAsync();
        await factory.Entered.Task.WaitAsync(AsyncOperationTimeout);
        cut.Render(parameters => parameters
            .Add(component => component.Request, Request("second.block", FileInteractionMode.View))
            .Add(component => component.ContentSource, Source("second"))
            .Add(component => component.Composition, composition)
            .Add(component => component.StateChanged, states.Add));
        states.Clear();
        await openingEditor.WaitAsync(AsyncOperationTimeout);

        Assert.Contains("second", cut.Find("[data-testid='interaction-text-view']").TextContent);
        Assert.Equal("View mode", Status(cut));
        Assert.DoesNotContain("first", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(states);
    }

    private IRenderedComponent<FileInteraction> RenderEditor(
        string fileName,
        string content,
        Action<FileInteractionSaveRequestedEventArgs>? save = null,
        Action<FileInteractionState>? stateChanged = null)
        => Render<FileInteraction>(parameters =>
        {
            parameters
                .Add(component => component.Request, Request(fileName))
                .Add(component => component.ContentSource, Source(content));
            if (save is not null)
            {
                parameters.Add(component => component.SaveRequested, save);
            }

            if (stateChanged is not null)
            {
                parameters.Add(component => component.StateChanged, stateChanged);
            }
        });

    private IRenderedComponent<FileInteraction> RenderEditor(
        string fileName,
        string content,
        Func<FileInteractionSaveRequestedEventArgs, Task> save,
        Action<FileInteractionState>? stateChanged = null)
        => Render<FileInteraction>(parameters =>
        {
            parameters
                .Add(component => component.Request, Request(fileName))
                .Add(component => component.ContentSource, Source(content))
                .Add(component => component.SaveRequested, save);
            if (stateChanged is not null)
            {
                parameters.Add(component => component.StateChanged, stateChanged);
            }
        });

    private static FileInteractionRequest Request(
        string fileName,
        FileInteractionMode mode = FileInteractionMode.Edit)
        => new(new FileReference("test", fileName), fileName, mode);

    private static DelegateContentSource Source(string text)
        => new((_, _) =>
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return ValueTask.FromResult(new FileContentLease(
                new MemoryStream(bytes, writable: false),
                "text/plain",
                bytes.Length,
                new FileContentRevision("r1")));
        });

    private static string? EditorValue(IRenderedComponent<FileInteraction> cut)
        => cut.Find("textarea").GetAttribute("value");

    private static string Status(IRenderedComponent<FileInteraction> cut)
        => cut.Find("[data-testid='interaction-save-status']").TextContent.Trim();

    private static FileInteractionComponentComposition TextComposition(
        FileInteractionProfileDescriptor profile)
        => new FileInteractionComponentBuilder()
            .AddProfile(profile)
            .AddRenderer(new FileInteractionRendererDescriptor(
                $"{profile.Id}-view", profile.Id, FileInteractionMode.View,
                typeof(TextFileView), FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                $"{profile.Id}-edit", profile.Id, FileInteractionMode.Edit,
                typeof(TextFileEditor), FileInteractionContentKind.Text))
            .Build();

    private sealed class BlockingHistoryFactory : IFileEditHistoryProviderFactory
    {
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CanCreate(FileInteractionProfileDescriptor profile, FileInteractionRequest request)
            => true;

        public async ValueTask<IFileEditHistoryProvider?> CreateAsync(
            FileInteractionProfileDescriptor profile,
            FileInteractionRequest request,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }

    private sealed class FixedHistoryFactory(IFileEditHistoryProvider provider)
        : IFileEditHistoryProviderFactory
    {
        public bool CanCreate(FileInteractionProfileDescriptor profile, FileInteractionRequest request)
            => true;

        public ValueTask<IFileEditHistoryProvider?> CreateAsync(
            FileInteractionProfileDescriptor profile,
            FileInteractionRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IFileEditHistoryProvider?>(provider);
    }

    private sealed class AlwaysAvailableHistoryProvider : IFileEditHistoryProvider
    {
        public int UndoCalls { get; private set; }

        public int RedoCalls { get; private set; }

        public FileEditHistoryState State { get; } = new(true, true, 1, 1);

        public ValueTask ResetAsync(
            FileReference file,
            FileContentRevision? baseRevision,
            FileEditSnapshot initialSnapshot,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask RecordAsync(
            FileEditSnapshot snapshot,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<FileEditSnapshot?> UndoAsync(CancellationToken cancellationToken = default)
        {
            UndoCalls++;
            return ValueTask.FromResult<FileEditSnapshot?>(null);
        }

        public ValueTask<FileEditSnapshot?> RedoAsync(CancellationToken cancellationToken = default)
        {
            RedoCalls++;
            return ValueTask.FromResult<FileEditSnapshot?>(null);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
