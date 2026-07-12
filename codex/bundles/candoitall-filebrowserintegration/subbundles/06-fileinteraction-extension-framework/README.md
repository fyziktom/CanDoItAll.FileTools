# SB06 — FileInteraction Extension Framework

## Status

- `Completed`
- Independent repair gate passed 2026-07-11; final cross-layer hardening is included in SB07 evidence.

## Objective

Implement UI-neutral interaction profile resolution, edit/revision state, save/preview strategies, and pluggable history coordination before any renderer shell.

## Success Criteria

- Profiles resolve deterministically by type/mode/capability/priority with explicit unsupported/ambiguity results.
- Manual/idle/interval/edit-count/text-unit save and preview debounce are cancellation/revision safe, including dynamic host-save availability.
- Text history is bounded and file/revision isolated; missing history disables undo/redo cleanly.
- All behavior is testable without Razor, filesystem, storage, or full host.

## Covered Inputs

- R012-R019,R029; N011-N015,N017.

## Prerequisites

- SB02/AC2 complete.

## Exact Source References

- `bundle://architecture/06-fileinteraction-design.md`
- `bundle://architecture/03-csharp-pattern-selection-records.md`
- `bundle://architecture/04-csharp-testability-plan.md`

## Deliverables

- FileInteraction.Core catalog/resolver.
- Edit session/state machine and revision-safe save acknowledgement.
- Delay abstraction/schedulers for autosave and preview.
- History catalog/coordinator plus bounded text history.
- DI registrations/builders and comprehensive unit/negative tests.

## Dependency Impact

- SB07 shell/renderers depend on these semantics; a UI-owned timer or giant type switch would invalidate modularity and testability.

## Validation Depth

- `Critical architecture foundation`.

## Implementation Steps

1. Implement profile catalog and documented match scoring.
2. Model edit/base/saved revisions and dirty/error/conflict transitions.
3. Implement save strategies with one in-flight save/coalescing/cancellation.
4. Implement preview debounce with stale-result rejection.
5. Implement history factory/state/branching/bounds/reset.
6. Add a framework-free composition builder/catalog; Components supplies the optional DI adapter in SB07.

## Scope Exceptions

- No Blazor shell/renderer, host persistence implementation, or full diff engine.

## Do Not Do

- Do not use timers, Task.Run, service location, component Type in core descriptors, storage writes, extension switch, or manually mark save success before awaited host completion.

## Acceptance Checklist

- [x] Deterministic profile selection/ambiguity/unsupported tests.
- [x] All autosave strategies validate/coalesce/cancel; conflicts require explicit resolution.
- [x] Stale save/preview cannot clear/replace newer edit and disposal drains/drops work safely.
- [x] History branch/bounds/file-reset plus monotonic shell-facing undo/redo tests.
- [x] New profile/history strategy adds through constructor-based composition without editing resolver/session switches.

## Proof Required

- `bundle://proof/SB06/manifest.md` and `semantic-invariants.md`.
- Failing-first stale-save/preview/ambiguity tests, passing transcript, hashes, source assertions, anti-stub audit, fake renderer registration downstream smoke.

## Browser Validation Logging

- N/A for core closure; SB07 supplies dependent browser proof.

## Progression Gate

- SB07 starts only after AC6 and architecture gate Pass.

## C# Architecture Impact

- Creates independent interaction algorithms instead of adding them to FileBrowser/component monoliths.

## Boundary Ownership

- FileInteraction.Core owns orchestration/policy; Abstractions contracts; Components renderers later.

## Dependency Direction

- FileInteraction.Core -> Abstractions only.

## Pattern Decision

- Catalog/factory, strategy, state/facade; rejected giant switches/timers/service locator.

## Testability Contract

- Direct deterministic unit tests with fake delay/save/preview/profile/history; no UI/network/filesystem.

## Partial Class Policy

- No partial runtime types.

## Architecture Proof Required

- Direct tests, extension-seam test, dependency graph, type responsibility metrics, gate Pass.

## Suggested Agent Prompt

```text
Implement SB06 only. Prove deterministic extensible interaction semantics entirely outside Razor/storage; block on stale revision or service-location shortcuts.
```
