# SB07 — Basic Viewers, Editing Workflow, and History

## Status

- `Completed`
- Closure gate passed after independent lifecycle, security, architecture, component, headed-browser, console/network, and visual repair on 2026-07-11.

## Objective

Implement the FileInteraction RCL, renderer registry, basic lightweight viewers/text editor, optional Markdown adapter, View/Edit/split preview, save/history UI, and end-to-end Browser -> Host -> Interaction sandbox flow.

## Success Criteria

- View/Edit mode selects only compatible registered renderers; unsupported/Diff states are explicit.
- Text, image, PDF/object, and optional Markdown render; Mermaid extension seam is demonstrated without a base dependency.
- Host save is awaited; errors retain dirty state; autosave/preview/history UI reflects core state.
- Renderer CSS/JS is isolated and loaded only when package/component is used.

## Covered Inputs

- R012-R023; N010-N016.

## Prerequisites

- SB05/AC5 browser activation proof and SB06/AC6 interaction core proof.

## Exact Source References

- `bundle://architecture/06-fileinteraction-design.md`
- `bundle://architecture/09-ui-assets-and-layout.md`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureSupportDialogs.razor`

## Deliverables

- FileInteraction.Components shell/toolbar/registry/context/built-ins.
- FileInteraction.Markdown optional package.
- Collocated modules/object URL lifecycle and isolated CSS.
- Component tests and Sandbox scenarios for view/edit/save/history/preview/unsupported/conflict.
- Host demo that opens FileInteraction from FileBrowser activation and persists to an in-memory/test sink.

## Dependency Impact

- SB08 package/architecture/visual closure depends on true modular renderer and host-save behavior.

## Validation Depth

- `Critical UI and behavior foundation`.

## Implementation Steps

1. Implement explicit renderer descriptor registry and DynamicComponent context.
2. Add shell states/mode switching/save/undo/redo and event-up persistence.
3. Add text view/edit, image, PDF/object, unsupported and optional Markdown profiles.
4. Implement split/debounced preview and object URL disposal.
5. Add demo Mermaid renderer registration without base dependency.
6. Add component tests and end-to-end Sandbox flows.
7. Run headed browser proof and visual tuning at desktop/floating/narrow sizes.

## Scope Exceptions

- Full diff, DOCX/XLSX/CSV/media editors and production Mermaid adapter remain future optional packages/host integration.

## Do Not Do

- Do not bundle every renderer dependency, write storage directly, use global scripts/styles, refresh preview per keystroke, clear dirty state on failed/stale save, or claim unsupported formats work.

## Acceptance Checklist

- [x] Resolver-to-renderer composition works without assembly scan/service location.
- [x] View/Edit/unsupported/Diff seam behavior proven.
- [x] Save success/failure/stale revision and autosave proven.
- [x] Undo/redo visibility/state/branching proven.
- [x] Preview coalesces and split layout is usable.
- [x] Optional Markdown absent from base dependency graph.
- [x] Browser-to-interaction flow passes real browser review.

## Proof Required

- `bundle://proof/SB07/manifest.md` and `semantic-invariants.md`.
- Failing-first save/preview/dependency tests, passing component/core transcripts, hashes/source/global-asset/anti-stub audits, Playwright actions/screenshots/review, end-to-end downstream smoke.

## Browser Validation Logging

- Route: Sandbox interaction matrix and browser-to-interaction scenario.
- Viewports: maximized first; 720x520, 560x360, 480x360, 390x844.
- Actions: open files of each basic type, View/Edit switch, rapid edit, preview, undo/redo, manual save, autosave, injected failure/retry, unsupported mode, close/switch file.
- Review: toolbar/hierarchy/readability, editor/preview split, scrolling, object/PDF/image sizing, dirty/error indicators, no clipping/lateral overflow, overlay layering.

## Progression Gate

- AC7 passed. SB08 was unlocked only after the 175-test interaction scope, focused CodeAnalytics snapshots, host-awaited save lifecycle, safe Markdown/object rendering, and headed interaction/browser-overlay matrix passed.

## C# Architecture Impact

- Establishes optional renderer packages and keeps rendering/persistence out of core/browser.

## Boundary Ownership

- Components render and emit; Core owns policy; host persists; optional package owns Markdown dependency.

## Dependency Direction

- Interaction.Components -> Interaction.Core + Abstractions; Markdown -> Components/Core; no reverse reference.

## Pattern Decision

- Renderer registry/factory plus EventCallback observer; rejected extension switch and all-in-one package.

## Testability Contract

- Registry/helpers/components tested with fake renderer/save/content; browser proves actual DOM/assets/layout.

## Partial Class Policy

- Cohesive Razor code-behind allowed; schedulers/history/resolution stay top-level Core types.

## Architecture Proof Required

- Dependency/source/global audit, component and browser proof, object/module disposal evidence, gate Pass.

## Suggested Agent Prompt

```text
Implement SB07 only. Deliver the modular event-up FileInteraction shell/basic renderers and prove save/history/preview plus real floating layouts; keep deferred formats explicit.
```
