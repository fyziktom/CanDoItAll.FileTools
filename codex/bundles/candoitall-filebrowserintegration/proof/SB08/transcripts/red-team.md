# SB08 Final Fake-Proof Red-Team — SB08-INV-01 SB08-INV-02 SB08-INV-03

- Run label: manual final closure gate after the first completed-validator pass, 2026-07-11.
- Working directory context only: bundle plus read-only evidence from the three scoped repositories.
- Command: `enumerate SB01-SB08 manifests/invariants/transcripts; re-read raw-note scope, production matrices, failing/passing proof, UI artifacts, package/graph/repository evidence, and deferred-main claims; run completed validator`.
ExitCode: 0

## Fake-proof resistance

| Attack | Evidence re-read | Result |
| --- | --- | --- |
| status/count-only completion | every SB01-SB08 manifest and semantic invariant exists; transcript counts are nonzero; completed validator resolves cited paths | Rejected |
| structure-only contracts/runtime | direct negative/positive test evidence covers validation/lifetimes, invalidation/source transitions, safe filesystem, stale UI events, save lifecycle, renderer security, and package graph | Rejected |
| production event proved only by a definition or seeded test | SB05 and SB07 matrices cite real producers, consumers, lifecycle attachment/retirement, and negative tests for host events and save/content completion | Rejected |
| screenshots without behavior or visual questions | SB05 and SB07 have headed actions, DOM/console/network assertions, original-resolution visual review, and frozen image hashes | Rejected |
| stale/active content hidden behind a generic renderer | hostile Markdown/SVG/unknown, object-URL overlap/readiness, binary/size, image and PDF negative cases are tied to production source/tests | Rejected |
| package claim based only on project files | exact 7+7 archive validator checks nuspec dependency/version, assemblies/docs/PDB/readme/license/assets, forbidden names, optional Markdig, and hashes | Rejected |
| transfer claimed while old owner/release entries remain | failing-first inventory shows 107 files; final diff removes exactly 107, modifies five integration/docs files, preserves the user file, and clean 9+9 remaining packages contain no FileBrowser/FileTools reference | Rejected |
| unrelated Components baselines hidden as success | BaseLib 68/69 approval and four WebGL BL0005 diagnostics are disclosed with unchanged full blob IDs; production builds/package ownership are separated | Rejected |
| main integration claimed from design prose | main source diff is zero; report and N003-N007/N009 rows say Partially solved; CI1-CI13 requires fresh implementation evidence | Rejected |
| unsafe future cache/authorization narrowed away | architecture rejects paths/unsigned tokens as authority, requires principal re-resolution/opaque handles, scopes keys, leaves process roots uncached, and gates distributed cache on durable shared revision | Rejected |
| “all resources” narrowed to local files | Resources/project/IPFS/external/FTP scenarios remain explicit; missing storage-object/IPFS connector is a named CI11 prerequisite | Rejected |
| broad analytics warnings misstated | final seven-product snapshot residuals are listed; clean focused snapshots/tests remain the semantic gate | Rejected |

## Mechanical audit

- SB01-SB08 each have a manifest and semantic invariant contract; transcript counts at audit time were 6, 6, 8, 4, 11, 3, 9, and 10 before this red-team file was added.
- no final report/subbundle status remains Pending, Not started, or In progress; the root readiness line remains correctly Ready.
- no production FileTools TODO, FIXME, or NotImplemented marker was found.
- every raw note is Solved or Partially solved with an exact proof/future exception.
- completed validator passed before this transcript; it is rerun after final hash/reference insertion.

## Decision

Pass. The proof rejects the known shallow implementations and preserves literal scope. Residuals are bounded and visible; none is used to claim CanDoItAll production integration or hostile-root filesystem security.
