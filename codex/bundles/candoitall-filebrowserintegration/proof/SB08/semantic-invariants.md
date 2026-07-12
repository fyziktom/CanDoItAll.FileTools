# SB08 Semantic Invariants

## Invariant SB08-INV-01

- Invariant ID: `SB08-INV-01`
- Source raw note: N001-N003/N015-N017 and R001-R004/R023/R030 require standalone package ownership and guarded Components cleanup only after proof.
- Expected behavior: seven selectively consumable FileTools packages validate exactly; legacy FileBrowser production/tests/sample/docs/release ownership is removed from Components after FileTools gates; remaining Components products/packages still validate within unchanged baseline limits.
- Disallowed shallow implementation: copy without ownership cleanup, delete before proof, FileTools reference from Components, package globbing that hides extra/stale artifacts, or all renderers/providers pulled into a base package.
- Failing-first test: `bundle://proof/SB08/transcripts/failing-first.md` records 107 tracked legacy ownership files before cleanup.
- Passing test: `bundle://proof/SB08/transcripts/filetools-validation.md`, `package-validation.md`, and `components-cleanup.md` prove product/package/transfer closure.
- Changed source files: FileTools solution/project/docs/scripts anchored by `repo://CanDoItAll.FileTools.slnx`; Components changed-file hashes are preserved in `bundle://proof/SB08/transcripts/components-cleanup.md`; package hashes are in `bundle://proof/SB08/transcripts/package-validation.md`.
- Production assertions: project and package validators reject foreign CanDoItAll dependencies, unexpected assets/packages, Markdig outside the optional package, and stale Components names.
- Red-team negative case: extra package, wrong nuspec dependency/version, global asset in non-RCL, missing symbol/doc/readme, stale FileBrowser release entry, or deletion of the preserved user file.
- Downstream dependency check: future CanDoItAll consumes FileTools through one-way references without requiring Components FileBrowser projects.

## Invariant SB08-INV-02

- Invariant ID: `SB08-INV-02`
- Source raw note: N004-N009/N017 and R024-R028 require detailed CanDoItAll integration/cache/revision design while main remains untouched.
- Expected behavior: every requested Projects, Workbench/folder, process-run, Resources, IPFS/filesystem/FTP surface has an exact future owner/anchor/scope/cache/action/security/test phase, and no production integration is claimed.
- Disallowed shallow implementation: generic “add provider later,” trusting a local path or encoded token as authority, one folder timestamp for mixed project sources, caching process folders, or editing main during refactoring.
- Failing-first test: `bundle://proof/SB08/transcripts/integration-design-audit.md` records missing current resource storage-object connector and the design gates that prevent a false complete implementation.
- Passing test: the same audit proves CI1-CI13 source anchors/owners/security/cache/revision/test gates; `bundle://proof/SB08/transcripts/main-readonly.md` proves no main source edit.
- Changed source files: design-only bundle files `bundle://architecture/07-candoitall-integration.md` and `bundle://architecture/08-cache-and-invalidation.md`; no CanDoItAll source file changed.
- Production assertions: none are claimed for future main integration; FileTools production remains storage-neutral and host-action only.
- Red-team negative case: cross-principal handle/cache reuse, Workbench opener reused as authorization, process output cached, resource promotion without re-resolution, or distributed cache enabled before durable shared revision.
- Downstream dependency check: future CI1-CI13 must refresh CodeAnalytics/source anchors and pass its own production tests; FileTools proof cannot be borrowed as main integration proof.

## Invariant SB08-INV-03

- Invariant ID: `SB08-INV-03`
- Source raw note: R029 requires architecture, browser, package, semantic proof, red-team, and completed validator gates.
- Expected behavior: all eight subbundles have portable manifests/invariants/transcripts, browser-visible work has reviewed screenshots/technical assertions, the C# gate matches final dependencies, and automated plus manual closure reject fake proof.
- Disallowed shallow implementation: status/count-only closure, missing transcript paths, screenshots without review, generated-source warnings hidden as zero, or deferred work called shipped.
- Failing-first test: early completed-validator runs failed on stale status/source references, missing semantic blocks/transcript metadata, and pending proof; those failures drove bundle repair.
- Passing test: `bundle://proof/SB08/transcripts/completed-validator.md` and `bundle://proof/SB08/transcripts/red-team.md` must record the final pass before closure.
- Changed source files: final bundle integrity set is recorded in `bundle://proof/SB08/transcripts/final-hashes.md`.
- Production assertions: final product snapshots/tests/packages and repository status evidence are tied to source/transcripts, not manually seeded feature state.
- Red-team negative case: remove a manifest path/hash/invariant id, restore a pending table row, use machine-only artifact paths, or claim the future CanDoItAll revision as emitted; the gate must fail.
- Downstream dependency check: the separate CanDoItAll integration run can re-enter from exact CI1-CI13 gates without reopening generic FileTools unless a contract gap is found.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| future project file-catalog revision | design contract only in `bundle://architecture/07-candoitall-integration.md`; no production producer in this run | future module provider/cache decorator design | CI phases require lifecycle and durable/shared revision before distributed cache | future integration tests reject folder-only revision and stale/cross-principal cache; absence of a producer prevents a shipped claim now |

## Anti-stub and literal closure

`bundle://proof/SB08/transcripts/anti-stub.md` distinguishes shipped FileTools/Components cleanup from future CanDoItAll work. `bundle://proof/SB08/transcripts/red-team.md` re-reads all manifests after artifacts freeze and rejects fake proof.
