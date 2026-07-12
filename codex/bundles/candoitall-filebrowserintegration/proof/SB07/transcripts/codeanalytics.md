# SB07 CodeAnalytics Evidence

- Run label: final focused product snapshots after extraction/cycle repair, 2026-07-11.
- Working directory context only: `C:/repositories/CanDoItAll.FileTools`.
- Command: `CodeAnalytics MCP analyze FileInteraction.Core and FileInteraction.Components product scopes; query diagnostics, Warning/Error findings, dependencies, and cycles`.
- Exit code: `0`.
ExitCode: 0

| Scope | Snapshot | Result |
| --- | --- | --- |
| FileInteraction.Core | `snap-20260711201550-e44d3e1b` | 0 diagnostics, 0 Warning/Error findings, 0 cycles; publisher uses an `object` event sender and no longer creates the initial concrete-type cycle |
| FileInteraction.Components | `snap-20260711201456-918bc3d5` | 0 diagnostics, 0 Warning/Error findings, 0 cycles |

The broad interim snapshot `snap-20260711200935-b810f295` predated the final extraction/cycle repair and included generated-`obj` noise; it is not presented as the closure snapshot. Final all-product evidence is `snap-20260711202431-5dbb5110` in `bundle://proof/SB08/transcripts/codeanalytics.md`.

This closes the architecture portions of `SB07-INV-01`, `SB07-INV-02`, and `SB07-INV-03` without claiming that the later broad snapshot has zero warnings.
