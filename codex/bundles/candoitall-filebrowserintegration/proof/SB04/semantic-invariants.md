# SB04 Semantic Invariants

## Validator contract record

- Invariant ID: `INV-SB04-SAFE-LIVE`
- Source raw note: N003/N005 and R022 require a simplified, current, safe filesystem example without storage-driver coupling.
- Expected behavior: browse and interaction reads are root-confined, cancellation/range aware, race/link resilient, path-redacted, and uncached; Open is host eligibility only.
- Disallowed shallow implementation: string-prefix path checks, executable URIs/actions, cached directory results, absolute-path identity, or unbounded whole-file reads.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first.md` records missing safe/live/content behavior.
- Passing test: `bundle://proof/SB04/transcripts/closure-summary.md` records 83 focused cases.
- Changed source files: `repo://src/CanDoItAll.FileTools.Providers.FileSystem/` with production hashes in `bundle://proof/SB04/manifest.md`.
- Production assertions: provider and content source share a root-confined reader; revisions are intentionally null for mutable filesystem data; no save/action/cache path exists.
- Red-team negative case: traversal, malformed token, root/file reparse, delete/replace race, inaccessible entry, cancellation, range bounds, and path disclosure.
- Downstream dependency check: SB05 live-folder mutation/refresh/file-host-event smoke passes through the adapter.

| Invariant | Evidence | Result |
|---|---|---|
| Authorization is the configured canonical root, never a display path. | traversal/rooted/noncanonical/foreign-source tests | Pass |
| Provider never follows a stable reparse occurrence. | root/link/exclusion/out-of-root tests and no link-follow API audit | Pass |
| Public projections do not contain the absolute root. | descriptor/item/error/display-name assertions | Pass |
| A browse enumerates fresh state and contains no adapter cache. | repeated-page mutation and metadata replacement tests; source audit | Pass |
| Range leases cannot read/seek beyond the requested range and own disposal. | range/seek/EOF/disposal/replacement/delete tests | Pass |
| FileInteraction reads reuse the root-confined reader but remain independent of browser-session state; mutable local reads claim no revision or write capability. | `FileInteractionContentSourceTests`; 83-case focused suite; provider/source audit | Pass |
| Files are host-invokable but the provider executes no host effect. | capability, null URI, no action-provider assertions | Pass |
| Cancellation is cooperative and is not normalized into a provider failure. | pre-cancel and read/browse cancellation tests | Pass |

Residual TOCTOU limits are explicit in `manifest.md`; they are not represented as a stronger guarantee.
