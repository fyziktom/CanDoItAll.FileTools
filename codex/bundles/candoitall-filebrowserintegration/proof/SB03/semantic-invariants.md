# SB03 Semantic Invariants

## Validator contract record

- Invariant ID: `INV-SB03-RETENTION`
- Source raw note: N004-N005/N009 and R005/R008-R009 require provider-neutral aggregation with disabled or bounded retention and explicit invalidation.
- Expected behavior: disabled retention always rereads, bounded retention is finite, invalidation retires in-flight generations, and source-set changes commit atomically with exact retry.
- Disallowed shallow implementation: a monolithic copied session with an unbounded dictionary, stale in-flight commit, or best-effort source transition.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first.md` records the absent target behavior and reopened 117-test result.
- Passing test: `bundle://proof/SB03/transcripts/closure-summary.md` records 132 focused cases.
- Changed source files: `repo://src/CanDoItAll.FileTools.FileBrowser.Core/Runtime/` and `repo://src/CanDoItAll.FileTools.FileBrowser.Core/Search/`; hashes are in `bundle://proof/SB03/manifest.md`.
- Production assertions: generation, transition, mode, continuation, navigation, search, store, and facade responsibilities have focused top-level owners.
- Red-team negative case: invalidate while load is in flight, cancel/supersede a source change, retry exactly, revisit after provider mutation, and cross browse/search query state.
- Downstream dependency check: SB05 host activation and live-folder Sandbox smoke use this runtime and passed.

| Invariant | Evidence | Result |
|---|---|---|
| Item/source/all invalidation is ordered before later work, cancels prior provider I/O, and prevents an invalidated completion from publishing or repopulating retained state. | blocked-refresh/invalidation and stale-publication regression tests | Pass |
| A source-set change is staged and committed atomically; caller cancellation or supersession leaves a coherent Idle snapshot. | cancellation/supersession source-transition tests | Pass |
| Retry after a failed source transition replays the exact failed transition. | exact transition retry test | Pass |
| An invalidated refresh reloads the base browse before reapplying every supported search scope. | parameterized invalidated-search refresh tests | Pass |
| Sort/filter changes during search keep the underlying browse query and later Load More/Clear behavior coherent. | sort/filter-during-search regression tests | Pass |
| Retained browse snapshots remember every observed continuation token, so an earlier token cannot be replayed after later pages. | accumulator retained-snapshot cursor-history test | Pass |
| A retired generation cancellation source is disposed only after all dependent operations drain and repeated supersession does not accumulate retired sources. | revision-guard lease/drain tests | Pass |
| Disabled retention keeps only active render state and revisits the provider; bounded retention remains reusable, bounded, and explicitly invalidatable. | direct loader/store mutation and diagnostics tests | Pass |
| Provider cancellation or malformed/stale pages cannot mutate visible or reusable state. | boundary, response-validation, and cancellation tests | Pass |
| Runtime responsibilities live in focused top-level owners and the session is a 275-line/33-member facade with no partial class. | CodeAnalytics `snap-20260711162248-cd589ee2`, source/metric audit | Pass |
