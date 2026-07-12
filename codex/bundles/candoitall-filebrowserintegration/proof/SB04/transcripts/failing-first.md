# Failing-First Filesystem Baseline — INV-SB04-SAFE-LIVE

- Target provider project is behavior-empty at entry.
- Legacy 47-test proof exists, but its files lack host Open capability, content/range reads, strong path redaction, and always-current no-follow semantics.
- Snapshot evidence: original `snap-20260711140859-32accfc4`; target `snap-20260711142636-c193e380`.

Passing proof must include current first-page mutation, bounded range, inert links, no absolute-root disclosure, host-only Open capability, malformed cursor/race/cancellation cases.
Command: `CodeAnalytics baseline and safe-filesystem adversarial requirement gate`
ExitCode: 1
