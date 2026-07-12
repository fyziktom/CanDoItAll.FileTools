# SB08 — Validation, Packaging, Transfer Closure, and CanDoItAll Integration Design

## Status

- `Completed`
- Product, package, browser, transfer, read-only main, architecture, red-team, and completed bundle gates passed 2026-07-11; CanDoItAll production integration remains the separate CI1-CI13 follow-up.

## Objective

Run full architecture/package/test/browser/red-team closure, remove FileBrowser ownership from Components only after proof, and finalize the source-anchored future CanDoItAll integration/cache/revision/module plan without modifying main.

## Success Criteria

- Clean standalone restore/build/test/package/run and post-change CodeAnalytics graph pass.
- Components transfer manifest matches, FileBrowser ownership/release entries are removed, and remaining Components solution builds/tests as scoped.
- All screenshots/analytics/raw notes/proof manifests/architecture gates/validators close honestly.
- Main repo has no task-authored change and every named integration use case has exact future phases/security/cache/proof.

## Covered Inputs

- R001-R030; N001-N017 final audit.

## Prerequisites

- SB03-SB07 Completed with manifests, AC3-AC7, browser analytics, and architecture gates.

## Exact Source References

- `bundle://architecture/07-candoitall-integration.md`
- `bundle://architecture/08-cache-and-invalidation.md`
- `bundle://architecture/10-transfer-and-packaging.md`
- `C:\repositories\CanDoItAll.Components\CanDoItAll.Components.slnx`
- `C:\repositories\CanDoItAll.Components\.github\workflows\ci.yml`
- `C:\repositories\CanDoItAll.Components\scripts\pack-release.ps1`
- `C:\repositories\CanDoItAll\src\Foundation\CanDoItAll.Infrastructure\Storage`
- `bundle://architecture/07-candoitall-integration.md`

## Deliverables

- Final FileTools README/API/integration/package docs and package artifacts validation.
- Refreshed CodeAnalytics inventory/dependencies/findings and architecture gate.
- Components source/docs/CI/release cleanup plus remaining build proof.
- Completed execution report/raw-note closure and final red-team verifier.
- Final CanDoItAll implementation phase plan/cache policy/revision/security/visual test matrix.

## Dependency Impact

- This is final closure and the handoff contract for the separate CanDoItAll integration run.

## Validation Depth

- `End-to-end architecture, UI, packaging, security, and bundle closure`.

## Implementation Steps

1. Run clean FileTools restore/build/test/package and Sandbox smoke.
2. Refresh CodeAnalytics and dependency/source/package/global-asset audits.
3. Rerun browser scenarios/screenshots and inspect them.
4. Compare transfer responsibility manifest, then remove Components ownership/references.
5. Build/test remaining Components scope and verify no unrelated edits.
6. Verify CanDoItAll git state unchanged and refresh plan anchors if refactoring moved them.
7. Audit N001-N017 literally, mark statuses, create explicit future phases for partial main rows.
8. Run architecture review, red-team manifests, completed validator, and sync README/report/gates.

## Scope Exceptions

- No CanDoItAll production source/DB/module UI implementation. Those rows close as Partially solved with the exact future plan.
- No publishing, commit, push, or PR without separate authorization.

## Do Not Do

- Do not delete Components source before proof, touch/reset/stage main changes, accept missing browser evidence, publish packages, or call planned main behavior shipped.

## Acceptance Checklist

- [x] Full FileTools commands pass from clean output.
- [x] Packages contain expected assemblies/assets/docs only.
- [x] CodeAnalytics reports zero project cycles/forbidden edges; generated-type duplicates and four cohesive-file complexity warnings are reviewed residuals, not hidden.
- [x] Browser matrix and screenshots pass review.
- [x] Components transfer/remaining production build/package ownership pass; unchanged test-baseline residuals are disclosed.
- [x] Main repo unchanged by task.
- [x] N001-N017 have evidence-backed final classifications; main-module rows remain explicitly design-only/deferred.
- [x] Red-team and completed validator pass.

## Proof Required

- `bundle://proof/SB08/manifest.md`, `semantic-invariants.md`, and final red-team verifier.
- Full transcripts/hashes/package listings/CodeAnalytics/source assertions/anti-stub/global asset/git status/Components cleanup/browser artifacts.
- Production Behavior Artifact Matrix for the planned file-catalog revision is explicitly marked future/not emitted in this run; it cannot be claimed as production proof.

## Browser Validation Logging

- Repeat SB05/SB07 critical routes/viewports after final packaging/cleanup; record actions, DOM assertions, screenshot paths, visual answers, and Pass/Fail.

## Progression Gate

- Bundle closes only when AC8, architecture gate Pass, red-team verifier, raw-note audit, and `validate_bundle.py --stage completed` all pass.

## C# Architecture Impact

- Proves final target graph/extension seams and removes obsolete ownership without weakening rollback safety.

## Boundary Ownership

- FileTools ships generic behavior; Components retains generic wrappers; main adapters/modules remain future owners described precisely.

## Dependency Direction

- Verified by direct project files and fresh CodeAnalytics; main remains one-way consumer in future design.

## Pattern Decision

- Confirm implementation matches PSR records; any drift reopens owning subbundle.

## Testability Contract

- Full direct tests/composition/browser/security proof; future main phases have named tests and cannot borrow FileTools proof as production integration proof.

## Partial Class Policy

- No unplanned partials; future Workbench uses child component/provider.

## Architecture Proof Required

- Final graph/findings, old-class shrink, no-partial, isolated tests, composition smoke, package/global audits, architecture gate Pass.

## Suggested Agent Prompt

```text
Implement SB08 only after all prerequisites. Close with fresh machine-checkable proof, guarded Components cleanup, and an honest read-only main integration handoff; reopen any weak foundation.
```
