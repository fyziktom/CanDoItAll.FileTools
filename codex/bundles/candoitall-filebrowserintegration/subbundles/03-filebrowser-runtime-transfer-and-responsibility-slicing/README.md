# SB03 — FileBrowser Runtime Transfer and Responsibility Slicing

## Status

- `Completed`
- Independent adversarial repair gate passed 2026-07-11.

## Objective

Transfer browser behavior/tests into FileBrowser.Core while replacing the concentrated session/tree design with focused, independently testable owners and explicit disabled/bounded retention/invalidation.

## Success Criteria

- Existing paging/search/navigation/selection/retry/cancellation behavior remains characterized.
- Session is a thin facade over top-level loader/navigation/search/selection/state-store services.
- Disabled retention and public invalidation return current provider data.
- Dynamic source-set revisions are handled safely.

## Covered Inputs

- R005-R009, R011, R029; N004-N009, N017.

## Prerequisites

- SB02/AC2 complete; transferred baseline tests and exact source manifest captured.

## Exact Source References

- `repo://src/CanDoItAll.FileTools.FileBrowser.Core/Runtime/FileBrowserSession.cs`
- `repo://src/CanDoItAll.FileTools.FileBrowser.Core/Runtime/FileBrowserStateStore.cs`
- `repo://src/CanDoItAll.FileTools.FileBrowser.Core/Search`
- `repo://tests/CanDoItAll.FileTools.FileBrowser.Core.Tests`
- `bundle://proof/SB03/transcripts/legacy-baseline-tests.md`
- `bundle://architecture/05-filebrowser-contract.md`

## Deliverables

- FileBrowser.Core runtime/search/catalog/validation implementations.
- Disabled/bounded `IFileBrowserStateStore` behavior and session invalidation APIs.
- Focused collaborators and smaller session facade.
- Transferred/expanded core tests and public registration/builder API where justified.

## Dependency Impact

- SB05 and final package proof depend on exact runtime semantics; stale retention or facade concentration invalidates process-run freshness and future extension claims.

## Validation Depth

- `Critical architecture foundation`.

## Implementation Steps

1. Capture baseline tests and public API inventory.
2. Transfer runtime/search tests and production source with namespace updates.
3. Introduce focused services/state-store policy without behavior drift.
4. Add disabled retention, invalidate source/item/all, and source-set revision handling.
5. Add adversarial stale completion/mutation/retry/cancellation tests.
6. Measure facade/type responsibilities and refresh CodeAnalytics.

## Scope Exceptions

- UI and concrete filesystem behavior are SB05/SB04.
- Host HybridCache is architecture-only in SB08.

## Do Not Do

- Do not split the session into partial files, inject IServiceProvider, add cache packages, or retain old behavior duplicated beside new owners.

## Acceptance Checklist

- [x] The 132-case characterization and adversarial suite passes with intentionally updated semantics.
- [x] Disabled retention observes provider mutation; bounded retention remains explicit and invalidatable.
- [x] Async item/source/all invalidation cancels older I/O and discards targeted reusable state.
- [x] Stale operations and superseded source transitions cannot publish or repopulate newer state.
- [x] The session facade is reduced from 1,315 lines/89 members to 275 lines/33 members.
- [x] Provider/search/store/source-transition behavior is composed through focused owners rather than a giant switch.

## Proof Required

- `bundle://proof/SB03/manifest.md` and `semantic-invariants.md`.
- Failing-first stale mutation/invalidation test, passing transcript, file hashes, type/member metrics, source assertion, anti-stub audit, fake-provider dependent smoke.

## Browser Validation Logging

- N/A for the Core closure. SB05 owns the downstream component/browser activation and live-folder smoke and does not borrow it into this gate.

## Progression Gate

- AC3 and the architecture gate passed. SB05 is unblocked; its activation/navigation smoke remains the dependent-flow check.

## C# Architecture Impact

- Real modular extraction from two large runtime owners.

## Boundary Ownership

- Core owns browser policies/orchestration; Abstractions owns shapes; UI/providers remain out.

## Dependency Direction

- FileBrowser.Core -> Abstractions only.

## Pattern Decision

- Thin facade, strategy, catalog/factory; rejected partial-class and flag-monolith designs.

## Testability Contract

- Loader/navigation/search/store/selection and facade tests use fake providers without UI/filesystem/full host.

## Partial Class Policy

- No runtime partial. Existing UI partial is outside scope.

## Architecture Proof Required

- Before/after type size, responsibility map, CodeAnalytics findings/dependencies, direct tests, no duplication, gate Pass.

## Suggested Agent Prompt

```text
Implement SB03 only. Preserve characterized browser semantics while creating real owners and disabled/invalidation behavior; block if the facade remains the monolith.
```
