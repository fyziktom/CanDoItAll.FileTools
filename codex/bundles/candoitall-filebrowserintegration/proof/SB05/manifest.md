# SB05 Proof Manifest

Status: **Pass after independent repair and visual revalidation** (2026-07-11).

## Ownership

- Subbundle: SB05 Responsive FileBrowser Component and Sandbox.
- Requirements: R001, R006-R011, R021, R023.
- Raw notes: N001, N004-N005, N008, N010, N016.
- Semantic contract: `bundle://proof/SB05/semantic-invariants.md`.

## Review sequence

The initial RCL/browser result was independently reviewed rather than accepted from screenshots alone. The review rejected stale render callbacks and action-menu data, incomplete native keyboard activation, source-dependent chrome in the no-source state, menu semantics that claimed a richer keyboard model than implemented, duplicate warning keys, and low-height overflow. Repairs added render-time session/snapshot stamps, action-menu invalidation, native button Space behavior, no-source chrome suppression, honest native-popover group semantics, unique warning keys, and a single bounded result-scroll owner. The repaired result then passed component, architecture, browser, console, and visual review.

Failing-first/adversarial record: `bundle://proof/SB05/transcripts/failing-first.md`.

- Passing semantic positive proof: `bundle://proof/SB05/transcripts/passing-component-tests.md`.
- Anti-stub audit: `bundle://proof/SB05/transcripts/anti-stub.md`.
- Representative final source SHA-256: `113ee2f0b92b32a460d8ca25b959dea7d41b59bf9e7643e92afaa7c4e9f5b1fe` for `repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowser.razor.cs`.

## Commands and results

| Evidence | Result | Transcript |
| --- | --- | --- |
| FileBrowser.Components Release component tests | 43 passed, 0 failed, 0 skipped, exit 0 | `bundle://proof/SB05/transcripts/passing-component-tests.md` |
| Prior full RCL Release build | 0 warnings, 0 errors, exit 0 | `bundle://proof/SB05/transcripts/passing-build-and-format.md` |
| RCL `dotnet format --verify-no-changes` | clean, exit 0 | `bundle://proof/SB05/transcripts/passing-build-and-format.md` |
| CodeAnalytics `snap-20260711173023-3ec305d8` | 0 findings, diagnostics, open questions, hotspots, or cycles | `bundle://proof/SB05/transcripts/codeanalytics.md` |
| Source/host-boundary assertions | pass | `bundle://proof/SB05/transcripts/source-assertions.md` |
| Anti-stub/global-asset audit | pass | `bundle://proof/SB05/transcripts/anti-stub.md` |
| Playwright scenario/console/overflow matrix | pass; console 0 errors/0 warnings | `bundle://proof/SB05/transcripts/browser-validation.md` |
| Screenshot review | pass | `bundle://proof/SB05/transcripts/visual-review.md` |
| SB03/SB04 dependent activation/live-folder smoke | pass | `bundle://proof/SB05/transcripts/dependent-smoke.md` |

This closure-only bundle update did not rerun source-repository build/test/format commands; it records the final execution evidence supplied by the implementation/review pass and independently re-read the source, tests, CodeAnalytics snapshot, screenshots, hashes, and repository status without writing a source repository.

## Portable browser artifacts

- `repo://output/playwright/sb05/repaired-desktop-standard.png`
- `repo://output/playwright/sb05/action-popover.png`
- `repo://output/playwright/sb05/compact-focus-720x520.png`
- `repo://output/playwright/sb05/minimal-cards-focus-560x360.png`
- `repo://output/playwright/sb05/repaired-minimal-cards-480x360-final2.png`
- `repo://output/playwright/sb05/repaired-minimal-cards-390x360.png`
- `repo://output/playwright/sb05/repaired-minimal-cards-390x844-final2.png`

## Changed-file integrity

Final SHA-256 values for the RCL, component tests, Sandbox, final screenshots, and updated bundle records are captured in `bundle://proof/SB05/transcripts/final-hashes.md`. The FileTools repository is an uncommitted transfer worktree, so reliable per-file pre-SB05 hashes were not available; the transcript records this limitation instead of inventing a baseline and provides the final integrity set used by downstream review.

## Source assertions

- The RCL project references only FileTools Abstractions/Core plus `Microsoft.AspNetCore.App`; there is no Components/BaseLib or main CanDoItAll dependency.
- `FileBrowserInteractionDispatcher` and `FileBrowserInteractionGuard` reject callbacks captured from superseded session/snapshot render trees.
- `FileBrowserItemActions` invalidates loaded/in-flight actions when session, item object/key, source, revision, or disabled state changes.
- list/card primary controls are native buttons, so pointer, touch, and native Space activation reach the host for activatable non-selectable items; selectable items retain explicit select versus Enter/double-click activation behavior.
- a no-source snapshot renders the explicit empty source state without toolbar, location, search, or refresh controls.
- the action surface uses native `popover="auto"` top-layer behavior with `role="group"` and ordinary buttons; it does not claim menu/menuitem keyboard semantics.
- warning rendering keys by occurrence index, so equal warning identities/messages remain distinct render entries.
- there is no direct `href`, download, clipboard, window-open, navigation-manager, or provider action execution path in the RCL.

## Semantic adequacy gate

- Shallow-pass trap: responsive CSS and a populated event table could appear complete while stale callbacks still invoke a replaced file, provider actions execute inside the component, Space activation fails, menus clip, duplicate warnings crash, or the page itself scrolls in a 360px floating host.
- Adversarial negative proof: same-key session/snapshot replacement, in-flight action replacement, busy-state action disabling, non-selectable activatable items, duplicate warnings, no-source snapshots, and 480x360 overflow ownership are explicitly tested or measured.
- Semantic positive proof: a real Sandbox host navigates folders internally, receives file/action events without provider effects, renders standard/compact/minimal list/cards modes, and remains readable at desktop, floating, narrow, and low-height dimensions.
- Anti-stub result: no TODO/FIXME/NotImplemented/template-only marker or global JS asset was found in the production RCL.
- Literal closure: the generic reusable browser and its modes/event boundary are shipped; CanDoItAll module windows and FileInteraction remain correctly assigned to later work.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ItemInvoked` | `repo://src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserInteractionDispatcher.cs`; list/card native controls | `repo://samples/CanDoItAll.FileTools.Sandbox/Components/Pages/BrowserLab.razor` host event log | render-time session/snapshot stamp is captured before callback and revalidated before/after async work | `FileDoubleClick_AwaitsHostItemInvokedCallback`, `FolderEnter_NavigatesSessionWithoutInvokingHostViewer`, and both detached-callback tests in `repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserHostBoundaryBehaviorTests.cs` |
| `ActionRequested` | `repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserItemActions.razor.cs` through the root dispatcher | `repo://samples/CanDoItAll.FileTools.Sandbox/Components/Pages/BrowserLab.razor` host event log | action data invalidates on session/item/source/revision/busy change and the host callback is awaited | `ActionButton_AwaitsHostAndNeverExecutesSessionAction`, same-key replacement tests, and `SameKeyRevisionChange_CancelsAndRejectsInFlightActions` |

## Downstream decision

AC5 passes. SB07 later closed its browser-to-interaction dependency, and SB08 completed packaging, guarded transfer cleanup, and final regression closure.
