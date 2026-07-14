using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components;

public partial class FileInteraction : ComponentBase, IAsyncDisposable
{
    public const int DefaultMaximumContentBytes = 16 * 1024 * 1024;

    private readonly FileInteractionSurfaceBinding surface = new();
    private readonly FileInteractionSaveUiState saveUi = new();
    private readonly FileInteractionEditUiState editUi = new();
    private readonly FileInteractionEditingBinding editingBinding = new();
    private readonly FileInteractionStatePublisher statePublisher = new();
    private readonly FileInteractionEditCommandHandler editCommands;
    private readonly FileInteractionModeController modeController;
    private readonly FileInteractionPreviewEventBridge previewEvents;
    private readonly FileInteractionSaveEventBridge saveEvents;
    private int generation;
    private bool showPreview;
    private bool disposed;

    public FileInteraction()
    {
        editCommands = new FileInteractionEditCommandHandler(
            saveUi,
            editUi,
            () => editing,
            () => generation,
            () => SaveRequested.HasDelegate,
            IsCurrent,
            PublishStateAsync);
        previewEvents = new FileInteractionPreviewEventBridge(
            () => editing?.Preview,
            () => disposed,
            work => InvokeAsync(work),
            StateHasChanged,
            error => DispatchExceptionAsync(error));
        saveEvents = new FileInteractionSaveEventBridge(
            saveUi,
            () => editing,
            IsCurrent,
            work => InvokeAsync(work),
            StateHasChanged,
            PublishStateAsync,
            error => DispatchExceptionAsync(error));
        modeController = new FileInteractionModeController(
            surface,
            () => disposed,
            () => generation,
            () => ModeChanged,
            IsCurrent,
            PublishStateAsync,
            EnsureEditingAsync);
    }

    /// <summary>
    /// Host-authoritative interaction request. When handling <see cref="ModeChanged"/>, render a new
    /// request carrying the accepted mode; otherwise a controlled parent render may restore this mode.
    /// </summary>
    [Parameter, EditorRequired]
    public FileInteractionRequest? Request { get; set; }

    [Parameter, EditorRequired]
    public IFileContentSource? ContentSource { get; set; }

    [Parameter]
    public FileInteractionComponentComposition? Composition { get; set; }

    [Parameter]
    public EventCallback<FileInteractionSaveRequestedEventArgs> SaveRequested { get; set; }

    /// <summary>Raised and awaited before mode-state publication so a controlled host can update Request.Mode.</summary>
    [Parameter]
    public EventCallback<FileInteractionMode> ModeChanged { get; set; }

    [Parameter]
    public EventCallback<Exception> LoadFailed { get; set; }

    [Parameter]
    public EventCallback<FileInteractionState> StateChanged { get; set; }

    [Parameter]
    public int MaximumContentBytes { get; set; } = DefaultMaximumContentBytes;

    [Parameter]
    public bool AllowModeSwitch { get; set; } = true;

    [Parameter]
    public string? AriaLabel { get; set; }

    [Parameter]
    public bool FillAvailableHeight { get; set; }

    public FileInteractionState CurrentState =>
        FileInteractionStateFactory.Create(surface, editing, saveUi, editUi);

    private FileInteractionEditingRuntime? editing => editingBinding.Current;

    private ReadOnlyMemory<byte> CurrentContent => editing?.Content ?? surface.Content;

    private string? CurrentText => editing?.Text ?? surface.Text;

    private Dictionary<string, object> RendererParameters => new()
    {
        [nameof(IFileInteractionRendererComponent.Context)] = FileInteractionRenderContextFactory.CreateMain(
            surface,
            editing,
            surface.Mode,
            CurrentContent,
            CurrentText,
            MaximumContentBytes,
            FileInteractionRendererEventFactory.CreateTextChanged(
                this,
                surface.Mode,
                surface.Renderer,
                editing,
                generation,
                editCommands.HandleTextChangedAsync),
            FileInteractionRendererEventFactory.CreateContentChanged(
                this,
                surface.Mode,
                editing,
                generation,
                editCommands.HandleContentChangedAsync))
    };

    private Dictionary<string, object> PreviewRendererParameters => new()
    {
        [nameof(IFileInteractionRendererComponent.Context)] = FileInteractionRenderContextFactory.CreatePreview(
            surface,
            editing ?? throw new InvalidOperationException("No file editor is active."),
            MaximumContentBytes)
    };

    protected override async Task OnParametersSetAsync()
    {
        if (Request is null)
        {
            throw new InvalidOperationException($"{nameof(Request)} is required.");
        }

        if (ContentSource is null)
        {
            throw new InvalidOperationException($"{nameof(ContentSource)} is required.");
        }

        if (MaximumContentBytes <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaximumContentBytes)} must be positive.");
        }

        statePublisher.SetCallback(StateChanged);
        var composition = Composition ?? FileInteractionComponentComposition.BuiltIn;
        if (surface.HasInputChanged(Request, ContentSource, composition, MaximumContentBytes))
        {
            await ReloadAsync(Request, ContentSource, composition);
        }
        else if (surface.Mode != Request.Mode)
        {
            if (surface.State == FileInteractionLoadState.Loaded)
            {
                await modeController.ChangeAsync(Request.Mode, notifyHost: false);
            }
            else
            {
                await ReloadAsync(Request, ContentSource, composition);
            }
        }

        editing?.NotifySaveAvailabilityChanged();

        await PublishStateAsync();
    }

    private async Task ReloadFromUiAsync()
    {
        if (Request is not null && ContentSource is not null)
        {
            await ReloadAsync(Request, ContentSource, Composition ?? FileInteractionComponentComposition.BuiltIn);
        }
    }

    private async Task ReloadAsync(
        FileInteractionRequest request,
        IFileContentSource source,
        FileInteractionComponentComposition composition)
    {
        var currentGeneration = ++generation;
        surface.CancelLoad();
        await ResetEditingAsync();
        if (disposed || currentGeneration != generation)
        {
            return;
        }

        saveUi.Reset();
        editUi.Reset();
        var loading = surface.LoadAsync(
            request,
            source,
            composition,
            MaximumContentBytes,
            () => IsCurrent(currentGeneration));
        await PublishStateAsync();
        var outcome = await loading;
        if (!outcome.IsCurrent)
        {
            return;
        }

        if (outcome.Error is not null)
        {
            var loadFailed = LoadFailed;
            await PublishStateAsync();
            await Task.Yield();
            if (!IsCurrent(currentGeneration))
            {
                return;
            }

            if (loadFailed.HasDelegate)
            {
                await loadFailed.InvokeAsync(outcome.Error);
            }
        }

        if (!IsCurrent(currentGeneration))
        {
            return;
        }

        if (surface.State == FileInteractionLoadState.Loaded
            && surface.Mode == FileInteractionMode.Edit)
        {
            await EnsureEditingAsync();
        }

        if (IsCurrent(currentGeneration))
        {
            await PublishStateAsync();
        }
    }

    private async Task ChangeModeAsync(FileInteractionMode mode)
        => await modeController.ChangeAsync(mode, notifyHost: true);

    private bool CanUseMode(FileInteractionMode mode)
        => (AllowModeSwitch || mode == surface.Mode) && surface.CanUseMode(mode);

    private async Task EnsureEditingAsync()
    {
        if (surface.Profile is null
            || surface.Request is null
            || surface.Renderer is null)
        {
            return;
        }

        if (editing is not null
            && string.Equals(editing.Profile.Id, surface.Profile.Id, StringComparison.Ordinal))
        {
            return;
        }

        var operationGeneration = generation;
        var targetProfile = surface.Profile;
        var targetRenderer = surface.Renderer;
        var targetRequest = surface.Request;
        var targetComposition = surface.Composition;
        var initialContent = CurrentContent.ToArray();
        var saveTarget = new EventCallbackFileSaveTarget(
            () => SaveRequested,
            request => saveEvents.OnHostSaveStartingAsync(request, operationGeneration));
        var created = await editingBinding.ReplaceAsync(
            targetProfile,
            targetRenderer.ContentKind,
            targetRequest,
            targetComposition,
            initialContent,
            MaximumContentBytes,
            saveTarget,
            () => IsCurrent(operationGeneration) && SaveRequested.HasDelegate,
            () => IsCurrent(operationGeneration)
                && ReferenceEquals(surface.Profile, targetProfile)
                && surface.Request == targetRequest);
        if (created is null)
        {
            return;
        }

        saveUi.Reset();
        editUi.Reset();
        saveEvents.Attach(created, operationGeneration);
        if (created.Preview is not null)
        {
            previewEvents.Attach(created.Preview);
            showPreview = created.Preview.ShowByDefault;
        }
    }

    private void TogglePreview()
    {
        if (surface.Mode == FileInteractionMode.Edit && editing?.Preview is not null)
        {
            showPreview = !showPreview;
        }
    }

    private bool IsCurrent(int operationGeneration)
        => !disposed && Volatile.Read(ref generation) == operationGeneration;

    private ValueTask PublishStateAsync()
        => statePublisher.PublishAsync(CurrentState);

    private bool IsCurrent(
        int operationGeneration,
        FileInteractionEditingRuntime runtime)
        => IsCurrent(operationGeneration) && ReferenceEquals(editing, runtime);

    private async ValueTask ResetEditingAsync()
    {
        var previous = editingBinding.Current;
        if (previous is not null)
        {
            saveEvents.Detach();
            if (previous.Preview is not null)
            {
                previewEvents.Detach(previous.Preview);
            }
        }

        await editingBinding.ResetAsync();

        showPreview = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        generation++;
        surface.CancelLoad();
        await ResetEditingAsync();
        await surface.DisposeAsync();
    }

}
