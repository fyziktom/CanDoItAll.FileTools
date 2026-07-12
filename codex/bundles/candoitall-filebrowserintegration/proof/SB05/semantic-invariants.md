# SB05 Semantic Invariants

## Validator contract record

- Invariant ID: `SB05-INV-01`
- Source raw note: N008/N010/N016 and R006-R011/R021 require responsive browser modes with folder-internal navigation and host-only file/action effects.
- Expected behavior: callbacks are bound to the exact rendered session/snapshot, folder/file/action semantics are distinct, operational states are honest, and only the result region scrolls in constrained hosts.
- Disallowed shallow implementation: direct anchors/effects, key-only stale checks, clickable non-button rows, false menu semantics, or page-level overflow hidden by clipping.
- Failing-first test: `bundle://proof/SB05/transcripts/failing-first.md` records the independent defects that reopened the initial UI.
- Passing test: `bundle://proof/SB05/transcripts/passing-component-tests.md` plus `bundle://proof/SB05/transcripts/browser-validation.md` prove repaired component and real browser behavior.
- Changed source files: `repo://src/CanDoItAll.FileTools.FileBrowser.Components/` with hashes in `bundle://proof/SB05/transcripts/final-hashes.md`.
- Production assertions: render-time guards, native buttons, native top-layer popover, isolated CSS, and awaited host dispatch are in production RCL code.
- Red-team negative case: same-key replacement, in-flight stale actions, duplicate warnings, no source, native Space, 480x360 scroll ownership, and open overlay clipping.
- Downstream dependency check: SB07 browser-to-interaction activation uses the same host event seam; SB08 packages this RCL.

## Invariant contract

| Invariant | Raw note / requirement | Expected behavior | Disallowed shallow implementation | Failing-first and passing proof | Production/source proof | Downstream check | Result |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `SB05-INV-01` Host-only effects | N010; R006-R007 | folders navigate inside the session; files and descriptive actions are awaited host events; the RCL executes no provider/browser effect | executable URI/anchor/copy/download or session action hidden behind a button | `failing-first.md`; 43-case suite including `FileDoubleClick_AwaitsHostItemInvokedCallback`, `FolderEnter_NavigatesSessionWithoutInvokingHostViewer`, `ActionButton_AwaitsHostAndNeverExecutesSessionAction`; `dependent-smoke.md` | dispatcher, item actions, Sandbox host event log, direct-effect source audit | Browser activation is a valid upstream seam for SB07 | Pass |
| `SB05-INV-02` Render-time freshness | N005,N010; R006,R009 | every selection/activation/action callback is bound to the session and snapshot that rendered it and cannot act after replacement | checking only item key, or capturing freshness after the callback begins | `failing-first.md`; detached same-key session and same-session/snapshot replacement tests; `SameKeyRevisionChange_CancelsAndRejectsInFlightActions` | `FileBrowserInteractionDispatcher`, `FileBrowserInteractionGuard`, render-created callbacks | stale FileBrowser events cannot open the wrong FileInteraction file | Pass |
| `SB05-INV-03` Accessible honest interaction | N008,N010; R006,R010-R011 | activatable non-selectable items use native button pointer/touch/Space activation; selectable items keep select vs Enter/double-click behavior; popover semantics match the implemented keyboard model | clickable div/manual key filter that misses Space, or `role=menu`/`menuitem` without full menu keyboard behavior | `failing-first.md`; `ActivatableNonSelectable_PointerTouchOrNativeSpaceClickInvokesHost`, `SelectableItem_ClickSelects_EnterAndDoubleClickActivate`, `ActionPopover_UsesNativeTopLayerWithSimpleButtonGroupSemantics` | list/card native buttons; `popover=auto`, `role=group`, ordinary action buttons | keyboard and popup behavior is usable by future hosts | Pass |
| `SB05-INV-04` Complete operational states | N004-N005,N008; R010-R011 | no-source state hides unusable controls; busy state disables loaded actions; duplicate warning identities render independently; source changes dispatch through a real control | leaving dead search/refresh chrome, stale enabled actions, or warning key collisions | `failing-first.md`; no-source, busy-action, duplicate-warning, and multi-source rendered tests | root conditional markup, unique warning occurrence keys, item-action invalidation | Sandbox states are trustworthy for SB08 regression | Pass |
| `SB05-INV-05` Responsive bounded layout | N008; R010-R011 | Standard/Compact/Minimal and List/Cards remain readable across 1440x900, 720x520, 560x360, 480x360, 390x360, and 390x844; overlays use top layer; only the result region scrolls in the constrained card case | hide controls by clipping or let body/root/card each compete as scroll owners | `failing-first.md`; `browser-validation.md`; `visual-review.md`; final repaired screenshots | component-scoped CSS and Sandbox focused frame | floating-window consumers can adopt Minimal without layout rework | Pass |
| `SB05-INV-06` Standalone RCL boundary | N001,N016; R001,R021,R023 | framework-native RCL points inward to FileTools Core/Abstractions and uses isolated CSS with no global JavaScript | retain Components.BaseLib, add document-global handlers, or grow policy back into a monolithic code-behind | Release build/format, contract tests, CodeAnalytics, asset/source audits | RCL `.csproj`, 362-line code-behind plus focused collaborators, `.razor.css`, no global asset directory or JavaScript | SB08 may later remove Components ownership after full closure | Pass |

Invariant ids appear in `bundle://proof/SB05/transcripts/failing-first.md` and `passing-component-tests.md`.

## Shallow-pass trap

A screenshot-only implementation could render attractive cards while still invoking stale files, executing provider effects, excluding Space users, misrepresenting popup semantics, colliding warning keys, or scrolling the entire floating host. The negative tests and DOM/overflow measurements are chosen specifically to fail that implementation.

## Adversarial negative proof

- Replace a session with an item having the same key/revision and invoke callbacks captured from the old render: no selection, navigation, item invocation, or action request is allowed.
- Replace an item at the same key while action loading is blocked: the old action list must not publish.
- Render an Open-only item with no Select capability: native pointer/touch/Space click must invoke the host without selecting.
- Render no source: toolbar, breadcrumb/location, search, and refresh must be absent.
- Render two warnings with the same identity: both messages must appear without a key collision.
- Open the action popover: it must be a top-layer button group, readable and unclipped, without false menuitem semantics.
- Constrain the host to 480x360: body/root dimensions remain 480x360 and only `.ft-file-browser__card-scroll` overflows.

## Semantic positive proof

The real Sandbox browser route shows multiple provider-neutral sources and live filesystem data, switches density/projection, navigates folders, logs file/action requests at the host boundary, exposes loading/empty/partial/retry states, and renders the final minimal cards layouts with usable search, recursion option, refresh/view controls, breadcrumb, cards, load-more, count, and status.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ItemInvoked` | `repo://src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserInteractionDispatcher.cs` | `repo://samples/CanDoItAll.FileTools.Sandbox/Components/Pages/BrowserLab.razor` | render-time stamp plus awaited callback | host-boundary detached/session/snapshot and folder-vs-file tests |
| `ActionRequested` | `repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserItemActions.razor.cs` | `repo://samples/CanDoItAll.FileTools.Sandbox/Components/Pages/BrowserLab.razor` | action load invalidation plus awaited host callback | no session execution, stale action, busy, and replacement tests |

## Anti-stub and literal closure

`bundle://proof/SB05/transcripts/anti-stub.md` records no production stub markers, global JS, or RCL `wwwroot`. The reusable browser requirement is closed without claiming the deferred CanDoItAll windows, resource/process adapters, FileInteraction renderers, or Components cleanup.
