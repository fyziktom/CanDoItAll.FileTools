namespace CanDoItAll.FileTools.FileInteraction.Core.Tests;

public sealed class FileSaveCoordinatorTests
{
    [Fact]
    public async Task SaveCompleted_SuccessObservesPostAcknowledgementState()
    {
        var target = new ControlledSaveTarget();
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        FileSaveCompletedEventArgs? completed = null;
        coordinator.SaveCompleted += (_, args) => completed = args;
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));

        var save = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Succeed(0, "base-1");
        var result = await save;

        Assert.NotNull(completed);
        Assert.Same(result, completed.Result);
        Assert.False(completed.State.IsSaving);
        Assert.False(completed.State.IsDirty);
        Assert.Equal(1, completed.State.SavedEditRevision);
        Assert.Equal("base-1", completed.State.BaseRevision?.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveCompleted_FailureAndConflictObserveRejectedDirtyState(bool conflict)
    {
        var target = new ControlledSaveTarget();
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        FileSaveCompletedEventArgs? completed = null;
        coordinator.SaveCompleted += (_, args) => completed = args;
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));
        Exception error = conflict
            ? new FileSaveConflictException(
                FileEditSessionTests.File(),
                new FileContentRevision("base-0"),
                new FileContentRevision("external"))
            : new IOException("write failed");

        var save = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Fail(0, error);
        var result = await save;

        Assert.NotNull(completed);
        Assert.Equal(
            conflict ? FileSaveOperationStatus.Conflict : FileSaveOperationStatus.Failed,
            completed.Result.Status);
        Assert.Same(result, completed.Result);
        Assert.Same(error, completed.State.LastSaveError);
        Assert.True(completed.State.IsDirty);
        Assert.False(completed.State.IsSaving);
        Assert.Equal(conflict, completed.State.HasConflict);
    }

    [Fact]
    public async Task SaveCompleted_CancellationObservesPostCancellationState()
    {
        var target = new ControlledSaveTarget();
        var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        FileSaveCompletedEventArgs? completed = null;
        coordinator.SaveCompleted += (_, args) => completed = args;
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));
        var save = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);

        await coordinator.DisposeAsync();
        var result = await save;

        Assert.NotNull(completed);
        Assert.Same(result, completed.Result);
        Assert.Equal(FileSaveOperationStatus.Cancelled, completed.Result.Status);
        Assert.True(completed.State.IsDirty);
        Assert.False(completed.State.IsSaving);
    }

    [Fact]
    public async Task SaveCompleted_ThrowingObserverCannotAlterOrHangPersistenceResult()
    {
        var target = new ControlledSaveTarget();
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        FileSaveCompletedEventArgs? observedAfterThrow = null;
        coordinator.SaveCompleted += (_, _) => throw new InvalidOperationException("observer failed");
        coordinator.SaveCompleted += (_, args) => observedAfterThrow = args;
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));

        var save = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Succeed(0, "base-1");
        var result = await save.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(FileSaveOperationStatus.Saved, result.Status);
        Assert.NotNull(observedAfterThrow);
        Assert.False(coordinator.State.IsDirty);
    }

    [Fact]
    public async Task SaveNow_EditDuringAwait_DoesNotClearNewerDirtyRevision()
    {
        var target = new ControlledSaveTarget();
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("revision-1"));

        var save = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("revision-2"));
        target.Succeed(0, "base-1");

        var result = await save;

        Assert.Equal(FileSaveOperationStatus.Saved, result.Status);
        Assert.Equal(2, coordinator.State.EditRevision);
        Assert.Equal(1, coordinator.State.SavedEditRevision);
        Assert.Equal("base-1", coordinator.State.BaseRevision?.Value);
        Assert.True(coordinator.State.IsDirty);

        var latestSave = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 2);
        Assert.Equal(2, target.Requests[1].EditRevision);
        Assert.Equal("base-1", target.Requests[1].ExpectedRevision?.Value);
        target.Succeed(1, "base-2");
        await latestSave;
        Assert.False(coordinator.State.IsDirty);
        Assert.Equal(2, coordinator.State.SavedEditRevision);
        Assert.Equal("base-2", coordinator.State.BaseRevision?.Value);
    }

    [Fact]
    public async Task SaveNow_NullPersistedRevision_ClearsObsoleteExpectedRevision()
    {
        var target = new ControlledSaveTarget();
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("revision-1"));

        var firstSave = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.SucceedWithoutRevision(0);
        await firstSave;

        Assert.Null(coordinator.State.BaseRevision);

        coordinator.ApplyEdit(FileEditSessionTests.Bytes("revision-2"));
        var secondSave = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 2);

        Assert.Null(target.Requests[1].ExpectedRevision);
        target.SucceedWithoutRevision(1);
        await secondSave;
    }

    [Fact]
    public async Task SaveNow_ConcurrentRequests_CoalesceAndNeverOverlap()
    {
        var target = new ControlledSaveTarget();
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));

        var first = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        var second = coordinator.SaveNowAsync().AsTask();
        target.Succeed(0, "base-1");

        Assert.Equal(FileSaveOperationStatus.Saved, (await first).Status);
        Assert.Equal(FileSaveOperationStatus.Saved, (await second).Status);
        Assert.Equal(1, target.Count);
        Assert.Equal(1, target.MaximumConcurrency);
        Assert.False(coordinator.State.IsDirty);
    }

    [Fact]
    public async Task SaveNow_TargetFailure_RetainsDirtyStateAndError()
    {
        var target = new ControlledSaveTarget();
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));

        var save = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Fail(0, new IOException("write failed"));
        var result = await save;

        Assert.Equal(FileSaveOperationStatus.Failed, result.Status);
        Assert.True(coordinator.State.IsDirty);
        Assert.IsType<IOException>(coordinator.State.LastSaveError);
        Assert.False(coordinator.State.HasConflict);
    }

    [Fact]
    public async Task SaveNow_Conflict_RetainsDirtyConflictState()
    {
        var target = new ControlledSaveTarget();
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));

        var save = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Fail(0, new FileSaveConflictException(
            FileEditSessionTests.File(),
            new FileContentRevision("base-0"),
            new FileContentRevision("external")));

        Assert.Equal(FileSaveOperationStatus.Conflict, (await save).Status);
        Assert.True(coordinator.State.IsDirty);
        Assert.True(coordinator.State.HasConflict);
    }

    [Fact]
    public async Task Interval_ConflictPausesPersistence_UntilExplicitRebase()
    {
        var delay = new ManualFileInteractionDelay();
        var target = new ControlledSaveTarget();
        var options = new FileAutoSaveOptions(FileAutoSaveTriggers.Interval, interval: TimeSpan.FromSeconds(1));
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target, options, delay);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));

        delay.ReleaseNext();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Fail(0, new FileSaveConflictException(
            FileEditSessionTests.File(),
            new FileContentRevision("base-0"),
            new FileContentRevision("external")));
        await TestWait.UntilAsync(() => coordinator.State.HasConflict && delay.ActiveCount == 1);

        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed-again"));
        Assert.True(coordinator.State.HasConflict);
        delay.ReleaseNext();
        await TestWait.UntilAsync(() => delay.ActiveCount == 1);
        Assert.Equal(1, target.Count);

        coordinator.ResolveConflictByRebasing(new FileContentRevision("external"));
        delay.ReleaseNext();
        await TestWait.UntilAsync(() => target.Count == 2);

        Assert.Equal("external", target.Requests[1].ExpectedRevision?.Value);
        target.Succeed(1, "base-2");
    }

    [Fact]
    public async Task ResolveConflictByOverwrite_NextSaveHasNoExpectedRevision()
    {
        var target = new ControlledSaveTarget();
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));
        var conflictedSave = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Fail(0, new FileSaveConflictException(
            FileEditSessionTests.File(),
            new FileContentRevision("base-0"),
            new FileContentRevision("external")));
        await conflictedSave;

        coordinator.ResolveConflictByOverwrite();
        var overwrite = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 2);

        Assert.Null(target.Requests[1].ExpectedRevision);
        target.Succeed(1, "base-overwrite");
        await overwrite;
    }

    [Fact]
    public async Task ChangeCount_ThresholdReached_RequestsAutomaticSave()
    {
        var target = new ControlledSaveTarget();
        var options = new FileAutoSaveOptions(FileAutoSaveTriggers.ChangeCount, changeCount: 2);
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target, options);

        coordinator.ApplyEdit(FileEditSessionTests.Bytes("one"));
        Assert.Equal(0, target.Count);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("two"));
        await TestWait.UntilAsync(() => target.Count == 1);

        Assert.True(target.Requests[0].IsAutomatic);
        target.Succeed(0, "base-2");
        await coordinator.WaitForPendingSavesAsync();
        Assert.False(coordinator.State.IsDirty);
    }

    [Fact]
    public async Task TextUnitCount_CumulativeReplacementUnitsRequestAutomaticSave()
    {
        var target = new ControlledSaveTarget();
        var options = new FileAutoSaveOptions(
            FileAutoSaveTriggers.TextUnitCount,
            textUnitCount: 5);
        await using var coordinator = new FileSaveCoordinator(
            FileEditSessionTests.CreateSession(),
            target,
            options);

        coordinator.ApplyEdit(FileEditSessionTests.Bytes("first"), changedTextUnits: 2);
        Assert.Equal(0, target.Count);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("second"), changedTextUnits: 3);
        await TestWait.UntilAsync(() => target.Count == 1);

        Assert.True(target.Requests[0].IsAutomatic);
        Assert.Equal(2, target.Requests[0].EditRevision);
        target.Succeed(0, "base-2");
        await coordinator.WaitForPendingSavesAsync();
    }

    [Fact]
    public async Task DueAutoSave_WhileUnavailableRemainsPendingUntilAvailabilityNotification()
    {
        var available = false;
        var target = new ControlledSaveTarget();
        var options = new FileAutoSaveOptions(
            FileAutoSaveTriggers.TextUnitCount,
            textUnitCount: 3);
        await using var coordinator = new FileSaveCoordinator(
            FileEditSessionTests.CreateSession(),
            target,
            options,
            canAutoSave: () => available);

        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"), changedTextUnits: 3);
        await TestWait.YieldSeveralAsync();

        Assert.Equal(0, target.Count);
        Assert.True(coordinator.State.IsDirty);
        Assert.Null(coordinator.State.LastSaveError);

        available = true;
        coordinator.NotifyAutoSaveAvailabilityChanged();
        await TestWait.UntilAsync(() => target.Count == 1);

        Assert.True(target.Requests[0].IsAutomatic);
        target.Succeed(0, "base-1");
        await coordinator.WaitForPendingSavesAsync();
    }

    [Fact]
    public async Task IntervalAttempt_WhileUnavailableWaitsForNextDelayAndNeverCallsTarget()
    {
        var available = false;
        var delay = new ManualFileInteractionDelay();
        var target = new ControlledSaveTarget();
        var options = new FileAutoSaveOptions(
            FileAutoSaveTriggers.Interval,
            interval: TimeSpan.FromSeconds(1));
        await using var coordinator = new FileSaveCoordinator(
            FileEditSessionTests.CreateSession(),
            target,
            options,
            delay,
            () => available);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));

        delay.ReleaseNext();
        await TestWait.UntilAsync(() => delay.ActiveCount == 1);

        Assert.Equal(0, target.Count);
        Assert.Null(coordinator.State.LastSaveError);

        available = true;
        coordinator.NotifyAutoSaveAvailabilityChanged();
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Succeed(0, "base-1");
    }

    [Fact]
    public async Task NegativeChangedTextUnits_RejectBeforeMutatingTheEditSession()
    {
        await using var coordinator = new FileSaveCoordinator(
            FileEditSessionTests.CreateSession(),
            new ControlledSaveTarget());

        Assert.Throws<ArgumentOutOfRangeException>(() => coordinator.ApplyEdit(
            FileEditSessionTests.Bytes("changed"),
            changedTextUnits: -1));
        Assert.Equal(0, coordinator.State.EditRevision);
        Assert.False(coordinator.State.IsDirty);
    }

    [Fact]
    public async Task Idle_RapidEdits_CancelsPriorDelayAndSavesLatestSnapshot()
    {
        var delay = new ManualFileInteractionDelay();
        var target = new ControlledSaveTarget();
        var options = new FileAutoSaveOptions(FileAutoSaveTriggers.Idle, idleDelay: TimeSpan.FromSeconds(1));
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target, options, delay);

        coordinator.ApplyEdit(FileEditSessionTests.Bytes("one"));
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("two"));
        Assert.Equal(1, delay.ActiveCount);
        delay.ReleaseNext();
        await TestWait.UntilAsync(() => target.Count == 1);

        Assert.Equal(2, target.Requests[0].EditRevision);
        target.Succeed(0, "base-2");
    }

    [Fact]
    public async Task Interval_DirtySession_SavesAndSchedulesNextInterval()
    {
        var delay = new ManualFileInteractionDelay();
        var target = new ControlledSaveTarget();
        var options = new FileAutoSaveOptions(FileAutoSaveTriggers.Interval, interval: TimeSpan.FromSeconds(5));
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target, options, delay);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));

        Assert.Equal(1, delay.ActiveCount);
        delay.ReleaseNext();
        await TestWait.UntilAsync(() => target.Count == 1);

        Assert.True(target.Requests[0].IsAutomatic);
        target.Succeed(0, "base-1");
        await TestWait.UntilAsync(() => delay.ActiveCount == 1);
    }

    [Fact]
    public async Task CompositeTrigger_ThresholdSaveMakesPendingIdleRequestANoOp()
    {
        var delay = new ManualFileInteractionDelay();
        var target = new ControlledSaveTarget();
        var options = new FileAutoSaveOptions(
            FileAutoSaveTriggers.Idle | FileAutoSaveTriggers.ChangeCount,
            idleDelay: TimeSpan.FromSeconds(2),
            changeCount: 1);
        await using var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target, options, delay);

        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));
        await TestWait.UntilAsync(() => target.Count == 1);
        target.Succeed(0, "base-1");
        await coordinator.WaitForPendingSavesAsync();
        delay.ReleaseNext();
        await TestWait.YieldSeveralAsync();

        Assert.Equal(1, target.Count);
    }

    [Fact]
    public async Task Dispose_SaveInFlight_CancelsTargetWithoutMarkingClean()
    {
        var target = new ControlledSaveTarget();
        var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("changed"));
        var save = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);

        await coordinator.DisposeAsync();
        var result = await save;

        Assert.Equal(FileSaveOperationStatus.Cancelled, result.Status);
        Assert.True(coordinator.State.IsDirty);
        Assert.False(coordinator.State.IsSaving);
    }

    [Fact]
    public async Task Dispose_SaveInFlightWithQueuedIntent_DropsQueueBeforeSecondTargetCall()
    {
        var target = new ControlledSaveTarget();
        var coordinator = new FileSaveCoordinator(FileEditSessionTests.CreateSession(), target);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("revision-1"));
        var first = coordinator.SaveNowAsync().AsTask();
        await TestWait.UntilAsync(() => target.Count == 1);
        coordinator.ApplyEdit(FileEditSessionTests.Bytes("revision-2"));
        var queued = coordinator.SaveNowAsync().AsTask();

        await coordinator.DisposeAsync();

        Assert.Equal(FileSaveOperationStatus.Cancelled, (await first).Status);
        Assert.Equal(FileSaveOperationStatus.Cancelled, (await queued).Status);
        Assert.Equal(1, target.Count);
    }
}
