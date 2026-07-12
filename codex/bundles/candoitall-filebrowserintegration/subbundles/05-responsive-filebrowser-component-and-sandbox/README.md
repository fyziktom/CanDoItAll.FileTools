# SB05 — Responsive FileBrowser Component and Sandbox

## Status

- `Completed`
- Closure gate passed after independent repair and visual revalidation 2026-07-11.

## Objective

Transfer and harden FileBrowser as a standalone framework-native RCL with host-only effects, explicit normal/compact/minimal modes, dynamic sources/selection events, and real floating/narrow visual proof.

## Success Criteria

- Folder activation navigates; file activation raises `ItemInvoked`.
- Every open/download/copy/custom effect raises a host event; no provider URI becomes an executable anchor.
- Normal/compact/minimal modes work in list/cards and low-height floating windows.
- Standalone Sandbox covers all important browser states and live filesystem flow.

## Covered Inputs

- R001,R006-R011,R021,R023; N001,N004-N005,N008,N010,N016.

## Prerequisites

- SB03/AC3 and SB04/AC4 manifests/gates complete.

## Exact Source References

- `repo://src/CanDoItAll.FileTools.FileBrowser.Components`
- `repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests`
- `repo://samples/CanDoItAll.FileTools.Sandbox`
- `bundle://proof/SB05/transcripts/legacy-baseline-tests.md`
- `bundle://architecture/09-ui-assets-and-layout.md`

## Deliverables

- FileBrowser.Components RCL with native semantic controls and isolated CSS/modules.
- Extracted UI coordination/projection helpers and component registration extensions.
- Explicit density/chrome/selection/source-update/event parameters.
- Expanded Sandbox scenarios and component tests.
- Playwright scripts/evidence for required viewports/states.

## Dependency Impact

- SB07 uses browser activation as its upstream flow; SB08 cannot remove Components ownership until this UI proof passes.

## Validation Depth

- `Critical UI foundation`.

## Implementation Steps

1. Capture current UI/helper tests and component/source manifest.
2. Transfer markup/styles/modules while removing Components.BaseLib source dependency.
3. Replace direct anchors/copy effects with host events.
4. Add normal/compact/minimal chrome/density and generic labels.
5. Add explicit selection/source revision callbacks and reduce code-behind responsibilities.
6. Expand sandbox for loading/empty/error/partial/paging/multi-source/live file activation.
7. Run component tests and headed browser loop across the proof matrix; tune until visually acceptable.

## Scope Exceptions

- The component does not show files itself; SB07/hosts own interaction.
- Main CanDoItAll floating window implementation is future work.

## Do Not Do

- Do not execute provider URIs, add global scripts/styles/body handlers, hide controls by clipping, leak “subprojects” into generic defaults, or close from screenshots without reviewing them.

## Acceptance Checklist

- [x] File event/folder navigation distinction proven.
- [x] No direct-effect anchors/source code.
- [x] All browser states and list/cards pass 43/43 Release component tests.
- [x] Native action popover open state is readable, unclipped, layered, and uses honest button-group semantics.
- [x] 1440x900,720x520,560x360,480x360,390x844/360px passes have no harmful overflow; at 480x360 only the card-results scroller overflows.
- [x] Browser-to-host activation and SB03/SB04 dependent smoke pass with console 0 errors/0 warnings.

## Proof Required

- `bundle://proof/SB05/manifest.md` and `semantic-invariants.md`.
- Failing-first direct-effect/file-event test; passing component transcripts; hashes/source/anti-stub; Playwright transcript/DOM assertions/screenshots/review; SB07 handoff smoke.

## Browser Validation Logging

- Route: Sandbox browser scenario matrix.
- Large first: maximized/at least 1440x900; then 720x520, 560x360, 480x360, 390x844/360px.
- Actions: navigate folders, switch sources/list/cards/modes, search/filter/sort, open menu, select, load more, trigger error/retry, mutate/refresh, double-click file.
- Review: text readability, hierarchy, gaps, toolbar collapse, breadcrumb, scroll owner, menu clipping/layering, status/selection, no lateral overflow.

## Progression Gate

- Pass. AC5 and the SB05 architecture/browser gates passed; SB07 subsequently closed its browser-to-interaction dependency and SB08 completed final regression, packaging, and transfer cleanup.

## Closure Evidence

- 43/43 Release component tests.
- Prior full RCL Release build: 0 warnings, 0 errors; `dotnet format --verify-no-changes`: clean.
- CodeAnalytics `snap-20260711173023-3ec305d8`: zero findings, diagnostics, open questions, hotspots, or cycles.
- Independent repairs: render-time session/snapshot stamps, stale action-menu invalidation, native Space activation, hidden no-source controls, honest popover group semantics, unique warning keys, and corrected low-height scroll ownership.
- Final browser proof: `repo://output/playwright/sb05/repaired-minimal-cards-480x360-final2.png` and `repo://output/playwright/sb05/repaired-minimal-cards-390x844-final2.png`; full matrix and review in `bundle://proof/SB05`.
- Manifest: `bundle://proof/SB05/manifest.md`; semantic contract: `bundle://proof/SB05/semantic-invariants.md`.

## C# Architecture Impact

- Removes cross-repo BaseLib dependency and separates UI policy from component coordination.

## Boundary Ownership

- RCL renders browser/session snapshots and emits events only.

## Dependency Direction

- FileBrowser.Components -> Abstractions + FileBrowser.Core + ASP.NET shared framework.

## Pattern Decision

- Observer/EventCallback and projection helpers; rejected executable provider anchors and monolithic code-behind.

## Testability Contract

- UI helpers direct-unit tested; component behavior via component tests; visual/layout truth via browser.

## Partial Class Policy

- Razor code-behind allowed only after non-rendering policies leave it; size/responsibility comparison required.

## Architecture Proof Required

- Dependency/source graph, code-behind metrics, event-only assertion, component/browser proof, gate Pass.

## Suggested Agent Prompt

```text
Implement SB05 only. Build the standalone event-up FileBrowser and prove real compact/floating UI; keep tuning while any screenshot or action assertion is wrong.
```
