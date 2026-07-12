# Requirement Traceability

| Requirement | Primary artifacts | Owning subbundle | Proof |
| --- | --- | --- | --- |
| R001-R004 | boundary/dependency/transfer architecture | SB01, SB08 | seven-package graph, exact 7+7 FileTools packages, 107-file guarded Components ownership removal, `proof/SB01`, `proof/SB08` |
| R005-R009 | FileBrowser contract/runtime/cache boundary | SB02, SB03 | 132-case runtime/invalidation/source-transition suite plus completed SB05 live/files/folder/host-event dependent smoke; `proof/SB03`, `proof/SB05` |
| R010-R011 | Browser modes/states | SB05 | 43/43 Release component tests, repaired headed Playwright/console/overflow matrix, reviewed screenshots, `proof/SB05` |
| R012-R019 | FileInteraction contracts/core | SB02, SB06, SB07 | 175 scoped interaction tests, awaited host-save lifecycle, resolver/history/preview/component evidence, focused clean snapshots |
| R020-R021 | basic/optional renderers and isolated assets | SB07 | safe Markdown/exact raster/inert SVG+unknown/object-URL tests, optional dependency/package audit, headed browser proof |
| R022 | filesystem safety/freshness | SB04 | 83 traversal/link/race/range/current-read/content-bridge tests and focused provider snapshot |
| R023 | standalone product/docs | SB01, SB08 | 433/433 full tests, Release 0/0, format clean, docs snippets, exact 7+7 package validation |
| R024 | main repo read-only | SB08 | branch/HEAD/same-11-skill-edits and zero Foundation/Modules/Processes source diff in `proof/SB08/transcripts/main-readonly.md` |
| R025-R028 | future main integration/cache/revision/use cases | SB08 | exact corrected module anchors/owners, native browse sidecars, authorization/handle contract, typed settings, cache/revision/security matrix, and note-by-note scenarios |
| R029 | gates/proof | all | eight manifests/invariant contracts, final architecture/red-team review, prepared/completed validators |
| R030 | guarded Components cleanup | SB08 | exactly 107 tracked deletions after FileTools proof; 9 production WAE builds, ordinary solution build, clean 9+9 remaining package proof; unchanged test-baseline residuals disclosed |

## Exact architecture destinations

- R001-R004: `architecture/01-csharp-boundary-map.md`, `02-csharp-dependency-direction.md`, `10-transfer-and-packaging.md`
- R005-R011: `architecture/05-filebrowser-contract.md`, `08-cache-and-invalidation.md`, `09-ui-assets-and-layout.md`
- R012-R021: `architecture/06-fileinteraction-design.md`, `03-csharp-pattern-selection-records.md`, `04-csharp-testability-plan.md`
- R022-R023: `architecture/01-csharp-boundary-map.md`, SB04/SB08
- R024-R028: `architecture/07-candoitall-integration.md`, `08-cache-and-invalidation.md`
- R029-R030: `plan/architecture-checkpoints.md`, `reviews/csharp-architecture-gate.md`, SB08

## Integration re-audit delta coverage

| Delta | Requirements | Durable destination | Future proof gate |
| --- | --- | --- | --- |
| Correct ProjectsBoard and Workbench toolbar paths | R025,R029 | `inputs/01-source-artifacts.md`, `architecture/07-candoitall-integration.md` | CI1 exact-source re-entry |
| Module-owned project/node/run/resource providers and prohibited reverse edges | R002,R004,R025 | `architecture/02-csharp-dependency-direction.md`, `architecture/07-candoitall-integration.md` | CI2 graph/cycle proof |
| Infrastructure-native browse sidecars remain FileTools-free | R002,R005,R025 | `architecture/07-candoitall-integration.md` | CI3 contracts/capabilities proof |
| Typed optional cache settings in `StorageCatalogRecord.ConfigJson` | R026,R027 | `architecture/07-candoitall-integration.md`, `architecture/08-cache-and-invalidation.md` | CI3 legacy/validation proof |
| Principal-aware authorization and opaque server handle registry | R006,R007,R015,R025-R027 | `architecture/07-candoitall-integration.md`, `architecture/08-cache-and-invalidation.md` | CI4 hostile path/token/cross-principal proof |
| Authorization-scope/runtime-profile dimensions in cache keys | R026-R028 | `architecture/08-cache-and-invalidation.md` | CI6 isolation/profile-switch proof |
| In-memory catalog revision now; durable/shared revision before distributed cache later | R026,R028 | `architecture/07-candoitall-integration.md`, `architecture/08-cache-and-invalidation.md` | CI6 restart/lifecycle proof; separate distributed gate |
| Projects filters shared across cards/files and deterministic source-set fingerprint | R025,R028 | `architecture/07-candoitall-integration.md` | CI7-CI8 hierarchy/source transition/browser proof |
| Workbench node scope does not reuse local opener as authorization | R006,R007,R025 | `architecture/07-candoitall-integration.md`, `analysis/02-assumptions-and-risks.md` | CI4 and CI9 hostile-metadata proof |
| Process run root policy moves to process ownership and remains uncached | R025,R027 | `architecture/07-candoitall-integration.md`, `architecture/08-cache-and-invalidation.md` | CI5 and CI10 mutation proof |
| Missing `resource.storage-object`/IPFS connector blocks complete promotion until added | R025,R028 | `architecture/07-candoitall-integration.md`, `traceability/02-input-coverage.md` | CI11 reauthorization/persistence proof |
| Corrected 13-phase CanDoItAll order | R024-R029 | `architecture/07-candoitall-integration.md`, `plan/architecture-checkpoints.md` | CI1-CI13 sequential unlocks |
