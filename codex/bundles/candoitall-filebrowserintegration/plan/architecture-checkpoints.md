# Architecture Checkpoints

| Checkpoint | After | Required evidence | Unlocks |
| --- | --- | --- | --- |
| AC1 Boundary | SB01 | projects compile; Abstractions zero dependencies; target graph documented | SB02 |
| AC2 Contracts | SB02 | provider/content/interaction/save/history contracts tested without UI/filesystem | SB03, SB04, SB06 |
| AC3 Browser runtime | SB03 | facade responsibility split, disabled/bounded retention, invalidation, transferred tests, no new partials | SB05 |
| AC4 FileSystem | SB04 | root/security/current-read negative proof and activation capability | SB05 |
| AC5 Browser UI | SB05 | host-only effects, compact/minimal browser proof and downstream activation smoke | SB07/SB08 |
| AC6 Interaction core | SB06 | resolver/scheduler/history tests independent of component/storage | SB07 |
| AC7 Interaction UI | SB07 | View/Edit/save/preview/history/basic renderers and browser proof | SB08 |
| AC8 Closure | SB08 | before/after CodeAnalytics, packages, Components cleanup, full tests/browser, integration plan audit | Final closure |

## Recorded checkpoint results

- AC1: Pass (`proof/SB01`).
- AC2: Pass (`proof/SB02`).
- AC3: Pass after independent adversarial repair (`proof/SB03`, 132/132 tests, CodeAnalytics `snap-20260711162248-cd589ee2`).
- AC4: Pass with the trusted-root residual documented (`proof/SB04`).
- AC5: Pass after independent repair (`proof/SB05`, 43/43 Release component tests, CodeAnalytics `snap-20260711173023-3ec305d8`, headed browser/console/overflow matrix).
- AC6: Pass after independent repair (`proof/SB06`).
- AC7: Pass after independent lifecycle, security, component, headed-browser, console/network, and visual repair (`proof/SB07`, 175 interaction-scope tests).
- AC8: Pass with explicit deferred main implementation and disclosed unrelated Components baselines (`proof/SB08`, 433 full tests, 7+7 FileTools packages, guarded transfer, red-team, completed validator).

## Partial-class review

Razor code-behind partials are allowed only when the component stays cohesive. No partial session/runtime/provider/catalog split is permitted. Any temporary partial requires a named removal step before its subbundle closes.

## Old-class shrink proof

- Existing `FileBrowserSession` behavior must either be distributed to focused top-level owners with a smaller facade or the gate blocks.
- Existing 534-line FileBrowser code-behind must shed non-rendering policy owners.
- Future CanDoItAll work must create child components/providers rather than expanding `ProjectStructurePage` partials or `RuntimeHostServiceCollectionExtensions` business behavior.

## Future CanDoItAll integration checkpoints

These are design-only re-entry checkpoints for the separate CanDoItAll implementation run. They do not change any SB/AC status above.

| Future checkpoint | After corrected phase | Required evidence | Unlocks |
| --- | --- | --- | --- |
| CI1 Re-entry graph | 1 | fresh scoped CodeAnalytics snapshots; exact `.csproj` graph; correct Projects/Workbench anchors; unchanged-main baseline | integration contracts |
| CI2 Contract boundary | 2 | FileTools refs plus Integration.Abstractions compile; no FileTools -> main, Infrastructure -> FileTools, Projects -> Workbench/Resources, Resources -> Workbench, or Processes -> Workbench edge | storage browsing |
| CI3 Native browse/settings | 3 | Infrastructure-native sidecars/registry; typed backward-compatible `ConfigJson` cache settings; capability/page/item bounds; Infrastructure remains FileTools-free | security adapters |
| CI4 Authorization/handles | 4 | principal-aware scope authorization; bounded opaque server handles; unsigned-token/path/existence/cross-principal negative tests; reauthorization on content/save | live sources |
| CI5 Live no-cache | 5 | filesystem and process-run mutation observed on next read; host cache and FileBrowser retention Disabled; no agent-write interception assumption | optional cache |
| CI6 Cache/revision | 6 | memory-primary HybridCache; Disabled pass-through; authorization/runtime-profile key separation; in-memory revision producer lifecycle; failed mutations do not bump; restart semantics documented | semantic scopes/UI |
| CI7 Project/node scopes | 7 | pure cycle-safe hierarchy resolver; Workbench-owned project/node providers; arbitrary metadata roots rejected; deterministic source-set fingerprint | Projects and Workbench UI |
| CI8 Projects UI | 8 | shared filters remain visible across Projects/Files; card dialog child; source-set transition and browser proof | Workbench UI |
| CI9 Workbench UI | 9 | focused `browse-files` coordinator; persisted `project-structure.files`; Compact/Minimal/low-height proof; Include subprojects; `open-local` remains separate | process UI |
| CI10 Process UI | 10 | neutral root policy in Processes.Application/integration core; focused dialog; managed/output/product authorization and freshness proof | resources |
| CI11 Resources | 11 | `resource.storage-object` or IPFS connector; Browse tab; re-resolve/reauthorize promotion; filesystem/IPFS/FTP proof; no `ResourceSourceSnapshotProvider` name collision | interaction migration |
| CI12 Interaction migration | 12 | incremental registered viewers/editors; opaque-handle reauthorization; awaited save/revision bump; no all-renderer dependency | closure |
| CI13 Closure | 13 | security/cache/browser/dependency/package gates; no new monolith/partial; distributed cache still disabled unless durable shared revision has independent proof | integration completion |

### Future unlock rules

- CI4 is a hard security foundation: no module UI may expose browsing before it passes.
- CI5 must prove uncached freshness before CI6 adds any cache decorator.
- CI7 backend scope ownership must pass before Projects or Workbench UI wiring; UI proof cannot substitute for boundary proof.
- CI11 cannot close Add as resource from IPFS/storage browsing until a persisted connector model exists and promotion reauthorizes the selected item.
- Enabling a distributed HybridCache secondary is a separate future checkpoint after CI13, gated by durable/shared revision and cross-node invalidation/profile-switch proof.
