namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>
/// Executes editor commands against a captured runtime and rejects completions after that runtime is superseded.
/// </summary>
internal sealed class FileInteractionEditCommandHandler
{
    private readonly FileInteractionSaveUiState saveUi;
    private readonly FileInteractionEditUiState editUi;
    private readonly Func<FileInteractionEditingRuntime?> getCurrentRuntime;
    private readonly Func<int> getGeneration;
    private readonly Func<bool> hostCanSave;
    private readonly Func<int, FileInteractionEditingRuntime, bool> isCurrent;
    private readonly Func<ValueTask> publishState;

    public FileInteractionEditCommandHandler(
        FileInteractionSaveUiState saveUi,
        FileInteractionEditUiState editUi,
        Func<FileInteractionEditingRuntime?> getCurrentRuntime,
        Func<int> getGeneration,
        Func<bool> hostCanSave,
        Func<int, FileInteractionEditingRuntime, bool> isCurrent,
        Func<ValueTask> publishState)
    {
        this.saveUi = saveUi ?? throw new ArgumentNullException(nameof(saveUi));
        this.editUi = editUi ?? throw new ArgumentNullException(nameof(editUi));
        this.getCurrentRuntime = getCurrentRuntime ?? throw new ArgumentNullException(nameof(getCurrentRuntime));
        this.getGeneration = getGeneration ?? throw new ArgumentNullException(nameof(getGeneration));
        this.hostCanSave = hostCanSave ?? throw new ArgumentNullException(nameof(hostCanSave));
        this.isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        this.publishState = publishState ?? throw new ArgumentNullException(nameof(publishState));
    }

    public Task HandleTextChangedAsync(
        string text,
        FileInteractionEditingRuntime runtime,
        int operationGeneration)
        => ApplyEditAsync(
            () => runtime.ApplyTextAsync(text, runtime.State.Current.MediaType),
            runtime,
            operationGeneration);

    public Task HandleContentChangedAsync(
        FileInteractionContentChange change,
        FileInteractionEditingRuntime runtime,
        int operationGeneration)
        => ApplyEditAsync(
            () => runtime.ApplyContentAsync(change),
            runtime,
            operationGeneration);

    public Task UndoAsync()
        => ApplyHistoryAsync(isUndo: true);

    public Task RedoAsync()
        => ApplyHistoryAsync(isUndo: false);

    public Task SaveAsync()
    {
        var runtime = getCurrentRuntime();
        var operationGeneration = getGeneration();
        return runtime is null
            ? Task.CompletedTask
            : SaveAsync(runtime, operationGeneration);
    }

    public Task RebaseConflictAsync()
        => ResolveConflictAndRetryAsync(overwrite: false);

    public Task OverwriteConflictAsync()
        => ResolveConflictAndRetryAsync(overwrite: true);

    private async Task ApplyEditAsync(
        Func<ValueTask> operation,
        FileInteractionEditingRuntime runtime,
        int operationGeneration)
    {
        if (!isCurrent(operationGeneration, runtime))
        {
            return;
        }

        FileInteractionOperationResult<bool> result;
        try
        {
            result = await FileInteractionOperationExecutor.ExecuteAsync(
                async () =>
                {
                    await operation();
                    return true;
                },
                () => isCurrent(operationGeneration, runtime));
        }
        catch (FileInteractionContentTooLargeException exception)
        {
            await ReportEditErrorAsync(exception, runtime, operationGeneration);
            return;
        }

        if (!result.IsCurrent)
        {
            return;
        }

        editUi.Reset();
        saveUi.Synchronize(runtime.State);
        await publishState();
    }

    private async Task ApplyHistoryAsync(bool isUndo)
    {
        var runtime = getCurrentRuntime();
        var operationGeneration = getGeneration();
        if (runtime is null)
        {
            return;
        }

        var outcome = await FileInteractionOperationExecutor.ExecuteAsync(
            () => isUndo ? runtime.UndoAsync() : runtime.RedoAsync(),
            () => isCurrent(operationGeneration, runtime));
        if (outcome is not { IsCurrent: true, Value: true })
        {
            return;
        }

        editUi.Reset();
        saveUi.Synchronize(runtime.State);
        await publishState();
    }

    private async Task SaveAsync(
        FileInteractionEditingRuntime runtime,
        int operationGeneration)
    {
        if (!isCurrent(operationGeneration, runtime)
            || !runtime.CanSave
            || !hostCanSave())
        {
            return;
        }

        var outcome = await FileInteractionOperationExecutor.ExecuteAsync(
            () => runtime.SaveAsync(),
            () => isCurrent(operationGeneration, runtime));
        if (outcome is { IsCurrent: true, Value: not null })
        {
            saveUi.Synchronize(runtime.State);
            await publishState();
        }
    }

    private async Task ResolveConflictAndRetryAsync(bool overwrite)
    {
        var runtime = getCurrentRuntime();
        var operationGeneration = getGeneration();
        if (runtime is null || saveUi.Error is not FileSaveConflictException conflict)
        {
            return;
        }

        if (!overwrite && !conflict.ActualRevision.HasValue)
        {
            return;
        }

        var outcome = overwrite
            ? await FileInteractionOperationExecutor.ExecuteAsync(
                () => runtime.ResolveConflictByOverwriteAsync(),
                () => isCurrent(operationGeneration, runtime))
            : await FileInteractionOperationExecutor.ExecuteAsync(
                () => runtime.ResolveConflictByRebasingAsync(conflict.ActualRevision!.Value),
                () => isCurrent(operationGeneration, runtime));
        if (!outcome.IsCurrent)
        {
            return;
        }

        saveUi.MarkDirty();
        await publishState();
        await Task.Yield();
        if (isCurrent(operationGeneration, runtime))
        {
            await SaveAsync(runtime, operationGeneration);
        }
    }

    private async Task ReportEditErrorAsync(
        Exception error,
        FileInteractionEditingRuntime runtime,
        int operationGeneration)
    {
        if (!isCurrent(operationGeneration, runtime))
        {
            return;
        }

        editUi.SetError(error);
        await publishState();
    }
}
