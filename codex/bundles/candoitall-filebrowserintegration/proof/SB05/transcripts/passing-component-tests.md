# SB05 Passing Component Tests
Command: `dotnet test tests/CanDoItAll.FileTools.FileBrowser.Components.Tests -c Release`
ExitCode: 0

- Run label: `2026-07-11 final repaired Release component suite`.
- Working directory: `C:\repositories\CanDoItAll.FileTools`.
- Command recorded by the execution pass:

```powershell
dotnet test tests\CanDoItAll.FileTools.FileBrowser.Components.Tests\CanDoItAll.FileTools.FileBrowser.Components.Tests.csproj -c Release
```

- Exit code: `0`.
- Result: `Passed: 43, Failed: 0, Skipped: 0, Total: 43`.

Behavior-level coverage tied to the invariant contract:

- `SB05-INV-01`: `FileDoubleClick_AwaitsHostItemInvokedCallback`, `FolderEnter_NavigatesSessionWithoutInvokingHostViewer`, `ActionButton_AwaitsHostAndNeverExecutesSessionAction`.
- `SB05-INV-02`: `DetachedCallbacks_AreRejectedAfterSameKeySessionReplacement`, `DetachedCallbacks_AreRejectedAfterSameSessionSameKeySnapshotReplacement`, `SameKeyRevisionChange_CancelsAndRejectsInFlightActions`, `LoadedActions_BecomeDisabledWithBusySnapshot`.
- `SB05-INV-03`: list/cards cases of `ActivatableNonSelectable_PointerTouchOrNativeSpaceClickInvokesHost`, `SelectableItem_ClickSelects_EnterAndDoubleClickActivate`, `ActionPopover_UsesNativeTopLayerWithSimpleButtonGroupSemantics`.
- `SB05-INV-04`: `NoSources_HidesSourceDependentToolbarAndNavigation`, `DuplicateWarningIdentity_RendersEveryWarningWithoutKeyCollision`, `MultiSourceNavigation_DispatchesSelectedSourceThroughRenderedControl`.
- `SB05-INV-05`: markup/style contracts for minimal low-height result space and native popover; browser metrics provide layout truth.
- `SB05-INV-06`: package graph, EventCallback-only events, no direct effects, scoped assets, responsive styles, operational states, and code-behind shrink contracts.

The test count is suite-level support; closure relies on the named adversarial/positive behaviors plus real browser evidence rather than the count alone.
