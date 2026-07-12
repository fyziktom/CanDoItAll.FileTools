# SB02 Semantic Invariants

## Validator contract record

- Invariant ID: `INV-SB02-CONTRACTS`
- Source raw note: N003/N010-N015 and R003/R005-R007/R012-R019 require storage-neutral browser and interaction contracts.
- Expected behavior: immutable validated identities, queries, content leases, profiles, save/history/preview contracts compile in BCL-only Abstractions with explicit lifetimes.
- Disallowed shallow implementation: DTO-shaped files with no validation, ownership, cancellation, or content-lifetime semantics.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first.md` proves the domain contracts were absent.
- Passing test: `bundle://proof/SB02/transcripts/passing-tests.md` proves 21 direct positive/negative/lifetime cases.
- Changed source files: `repo://src/CanDoItAll.FileTools.Abstractions/` with final hashes in `bundle://proof/SB02/manifest.md`.
- Production assertions: Abstractions has zero project/package/framework references and exposes no UI/filesystem/cache/storage implementation.
- Red-team negative case: reject malformed identities/options/unknown flag bits and prove leased streams dispose independently.
- Downstream dependency check: Browser Core, filesystem, and Interaction Core compile against these contracts without reverse edges.

## INV-SB02-CONTRACTS

- Source raw notes: N002/N003/N006/N007/N009-N016.
- Expected behavior: browser providers and interaction editors share validated, storage-neutral, BCL-only contracts with explicit content/save/history lifetimes and no UI/storage/cache dependency.
- Disallowed shallow implementation: DTO files that compile but accept invalid identity/ranges/capabilities, expose a browser-session stream as editor state, mutate caller buffers, or reference host/UI types.
- Failing-first: `bundle://proof/SB02/transcripts/failing-first.md` (no domain contract types existed).
- Passing: `bundle://proof/SB02/transcripts/passing-tests.md` and `passing-build.md`.
- Changed source/hashes: `bundle://proof/SB02/manifest.md`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.md`.
- Red-team negative: invalid/ambiguous contract combinations and forbidden dependencies are rejected by tests/source audit.
- Downstream check: every later product/test/Sandbox project compiles against the new Abstractions assembly.

## Shallow-pass trap

File existence or a zero-reference csproj is insufficient: a contract could still be semantically unsafe or force editor lifetime through FileBrowser. The 21 tests exercise validation, defensive copying, ownership, replayability, revision, and capability consistency.

## Adversarial negative proof

- unsafe active/malformed URIs fail;
- missing identities and invalid page/range bounds fail;
- edit-only, autosave-without-save, and preview-without-preview profiles fail;
- inconsistent history state/limits fail;
- mutable history input cannot alter the stored snapshot;
- forbidden application/framework assembly references fail.

## Semantic positive proof

Realistic provider items/queries and Markdown-like view/edit/save/preview/history descriptors normalize and preserve the values required by later runtime/UI packages. Replayable save streams and expected revisions support host persistence without a storage dependency.

## Anti-stub audit

See `bundle://proof/SB02/transcripts/anti-stub.md`; no placeholder production path exists.

## Raw-note literal closure

The contract surface is genuinely selectable and lightweight. Implementations of caching, browsing, history, saving, and rendering remain in their named downstream packages rather than being falsely claimed here.

## Production Behavior Artifact Matrix

SB02 introduces API value types and interfaces, not a production-emitted persisted signal/event. No producer/consumer lifecycle is claimed in this phase. The future project file-catalog revision is separately marked unimplemented and requires a real producer/consumer/lifecycle/negative matrix in the CanDoItAll integration phase.
