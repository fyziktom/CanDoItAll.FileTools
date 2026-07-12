# Failing-First Interaction Core Baseline — INV-SB06-EDIT-REVISION

- CodeAnalytics baseline `snap-20260711140220-e81d3243` detects no FileInteraction.Core domain type.
- Contracts exist after SB02, but there is no resolver, dirty/revision state, save scheduler, preview debounce, or history implementation.

Passing proof must reject ambiguous profiles and stale save/preview completions, coalesce configured triggers, bound/reset history, and instantiate every behavior without Razor/storage/full host.
Command: `CodeAnalytics baseline and independent lifecycle review of the initial FileInteraction.Core result`
ExitCode: 1
