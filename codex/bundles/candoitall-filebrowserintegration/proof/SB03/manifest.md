# SB03 Proof Manifest

Status: **Pass after independent adversarial repair** (2026-07-11).

- Semantic contract: `bundle://proof/SB03/semantic-invariants.md`.
- Failing-first/adversarial negative proof: `bundle://proof/SB03/transcripts/failing-first.md`.
- Passing semantic positive proof: `bundle://proof/SB03/transcripts/closure-summary.md`.
- Anti-stub audit: `bundle://proof/SB03/transcripts/closure-summary.md`.
- Portable source anchor: `repo://src/CanDoItAll.FileTools.FileBrowser.Core/Runtime/FileBrowserSession.cs`.

## Review sequence

The first transferred/sliced implementation passed 117 tests but was deliberately reopened. Independent review found gaps in invalidation versus in-flight work, cancelled/superseded source transitions, exact transition retry, retained continuation-token history, and browse/search query coherence. The repair introduced generation-backed invalidation, staged source transitions, full observed-cursor retention, and explicit mode/source coordinators; 15 focused regression cases raised the final suite to 132.

## Commands and results

- FileBrowser.Core focused Release tests: 132 passed, 0 failed, 0 skipped.
- FileTools Release build after the repair: 0 warnings, 0 errors.
- `dotnet format --verify-no-changes` checks: exit 0.
- Product CodeAnalytics snapshot `snap-20260711162248-cd589ee2`: three scoped projects, zero diagnostics and zero project cycles; FileBrowser.Core depends only on Abstractions and has no package dependency.
- Facade metric: `FileBrowserSession` is 275 physical lines / 33 analytics members, down from the legacy 1,315 lines / 89 members; no runtime partial was introduced.
- Independent architecture gate: Pass after the 117-test result was reopened, repaired, and re-reviewed.

## Responsibility result

- `FileBrowserSessionExecutionCoordinator` owns queued operation execution and commit ordering.
- `FileBrowserModeCoordinator` owns browse/search-mode transitions and query coherence.
- `FileBrowserSourceTransitionCoordinator` stages source-set changes for atomic commit and exact retry.
- `FileBrowserSourceRevisionGuard` owns generation cancellation and reference-counted retirement.
- `FileBrowserContinuationHistory` owns the complete set of observed continuation tokens.
- Loader, navigator, search coordinator, state store, selection, action dispatcher, and the session facade retain their focused responsibilities.

## Current behavior/repair artifacts and SHA-256

```text
beb6f1182334c0eade05f5de76d22fb4bd3e27a598c1295d054d54965b5ecfe9  Runtime/FileBrowserContinuationHistory.cs
780ff110f91df50828eae6a7c635071d4926b6dfea7232946dd0418f639846c8  Runtime/FileBrowserModeCoordinator.cs
7b1825ca480238562281c05bfb3cbe26ab994eb71d0eac547316db42dbfe487a  Runtime/FileBrowserLoader.cs
bad7b8d3dbbed8e5f51988825351f2abf81effc4d8905822c8c375909b69c650  Runtime/FileBrowserNavigator.cs
4838400890d6cf7cc69d7f75c26464b5e18ad492fbf3b3695783d6882a66571f  Runtime/FileBrowserSession.cs
2409ea9c373f46c758d0477c8edf019a172bb67411d59970674c759110a3d3bb  Runtime/FileBrowserSessionExecutionCoordinator.cs
8fb42249efae37280fd270e74ec9bfa7ffbbefcf3add47363a68ee071c760886  Runtime/FileBrowserSessionRuntime.cs
89dd7bf86e5add67c90f0837f2c90100df64c1e343165402a1e4a15fae0940d9  Runtime/FileBrowserSourceRevisionGuard.cs
9ca8343ec19a53cd49a1e715bcd6b0154a09a50d533de670444a1cfcd2154179  Runtime/FileBrowserSourceTransitionCoordinator.cs
96929ee061f0dbe2376a14eca001adb93822439bd08628fcc7538ab8b265223b  Runtime/FileBrowserStateStore.cs
cd57809913c91a889625ffcffd7038c2f7a555f1ad0cccb25f1ca307ab53edf9  Search/FileBrowserSearchCoordinator.cs
8f55429a534ae0abe17699ef3a1fcf33d529b6bf7f241bab04621f623794e7ee  Search/ProgressiveFileBrowserSearchStrategy.cs
47f93b8fc72dbe774c54a4da8fc9a75b8227d94c80361ad91994c231f0bb6151  tests/FileBrowserDynamicSourcesAndInvalidationTests.cs
53ed8388eef5a2089a2d4f7943dd385fcb5ea053018c98a90b37f1a7764f1b9e  tests/FileBrowserSessionBoundaryTests.cs
7c76e5fcc97600a606724d1eb135a08d96d750d8cd7eec863425d9bad7bf44fc  tests/FileBrowserSourceRevisionGuardTests.cs
```

Production paths are relative to `src/CanDoItAll.FileTools.FileBrowser.Core`; test paths are relative to `tests/CanDoItAll.FileTools.FileBrowser.Core.Tests`. Hashes were recomputed read-only from the final FileTools tree when this manifest was created.

## Dependent proof

SB05 owns FileBrowser.Components behavior and real browser smoke. Its downstream activation/navigation checks remain required for AC5, but the SB03 Core gate is complete and no longer blocks SB05.
