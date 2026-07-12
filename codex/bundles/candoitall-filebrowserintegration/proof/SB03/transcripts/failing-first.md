# Failing-First Runtime Baseline — INV-SB03-RETENTION

- Target CodeAnalytics baseline: `snap-20260711140220-e81d3243`.
- Result: FileBrowser.Core contains no declared production behavior type.
- Legacy evidence: `bundle://proof/SB03/transcripts/legacy-baseline-tests.md` proves the source behavior floor, but legacy has no disabled-retention/public-invalidation/source-set update API.

Required passing behavior: a revisit after provider mutation is current in Disabled mode, retained in Bounded mode, targeted invalidation removes stale state, and the session facade delegates provider I/O.
Command: `CodeAnalytics baseline plus adversarial review of the initial 117-test FileBrowser.Core result`
ExitCode: 1
