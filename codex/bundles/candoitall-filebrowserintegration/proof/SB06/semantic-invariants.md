# SB06 Semantic Invariants

## Validator contract record

- Invariant ID: `INV-SB06-EDIT-REVISION`
- Source raw note: N011-N014 and R012-R019 require UI-neutral profile/history/save/preview/edit policy with host persistence.
- Expected behavior: resolution is deterministic, edit/base revisions are monotonic, saves coalesce and remain host-acknowledged, conflicts require explicit resolution, previews debounce/reject stale work, and history is bounded/file scoped.
- Disallowed shallow implementation: first-match profile selection, overlapping saves, timer/task fire-and-forget, UI-owned history, or stale preview/save clearing a newer edit.
- Failing-first test: `bundle://proof/SB06/transcripts/failing-first.md` proves the runtime was absent and the initial 35-test result was later reopened.
- Passing test: `bundle://proof/SB06/transcripts/closure-summary.md` records the repaired Core suite and focused analytics.
- Changed source files: `repo://src/CanDoItAll.FileTools.FileInteraction.Core/` with final cross-layer hashes in `bundle://proof/SB07/transcripts/final-hashes.md`.
- Production assertions: explicit catalogs/coordinators own policy; no Razor/filesystem/cache/storage/main/DI implementation leaks into Core.
- Red-team negative case: disposal with queued save, conflict autosave, edit during save, equal-priority history/profile ties, cancellation-ignoring preview, and file/base revision switch.
- Downstream dependency check: SB07 components adapt these policies and the 175-test interaction scope passes.

| Invariant | Evidence | Result |
|---|---|---|
| Resolver order is exact MIME, MIME wildcard, extension, fallback; priority applies within a tier and ties are explicit ambiguity. | profile catalog positive/negative/parameterized MIME tests | Pass |
| Only one host save is active; callers coalesce without one wait token cancelling shared persistence. | concurrent/coalescing tests | Pass |
| Disposal cancels active persistence and drops queued intent before a second host call. | in-flight + queued disposal test | Pass |
| A stale save cannot clear a newer edit; unknown new revision clears the obsolete base. | edit-during-save and null-revision tests | Pass |
| Conflict is stable and blocks persistence until explicit rebase or overwrite. | interval conflict/rebase/overwrite tests | Pass |
| Undo/redo applies a new monotonic dirty edit without corrupting redo; ordinary edit truncates redo. | FileInteractionEditCoordinator tests | Pass |
| Preview is debounced, rejects stale completion, and disposal drains cancellation-ignoring work without publishing it. | preview rapid/stale/dispose tests | Pass |
| History is file/base-revision isolated and bounded by entry and byte budgets. | bounded history tests | Pass |
| Core composition is explicit and framework-free. | builder/catalog tests and dependency/source audit | Pass |
