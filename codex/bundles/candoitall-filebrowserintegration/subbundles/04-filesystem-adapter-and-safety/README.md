# SB04 — Filesystem Adapter and Safety

## Status

- `Completed`
- Focused gate and dependent Sandbox smoke passed 2026-07-11.

## Objective

Transfer the simplified local-filesystem provider as an optional, root-confined adapter and correct file activation/freshness semantics without adding host OS actions.

## Success Criteria

- Provider depends on Abstractions only where designed and supports shallow paging/metadata/range reads.
- Traversal, symlink/reparse escape, inaccessible/racing entries, malformed tokens, and cancellation are safe.
- Files advertise host invocation capability; no provider or component opens the OS/browser itself.

## Covered Inputs

- R006-R009, R022, R027; N003, N005, N009-N010.

## Prerequisites

- SB02/AC2 complete and original filesystem tests/source manifest captured.

## Exact Source References

- `repo://src/CanDoItAll.FileTools.Providers.FileSystem`
- `repo://tests/CanDoItAll.FileTools.Providers.FileSystem.Tests`
- `bundle://proof/SB04/transcripts/legacy-baseline-tests.md`
- `bundle://architecture/05-filebrowser-contract.md`

## Deliverables

- Providers.FileSystem project and options.
- Root/path/continuation/item/content implementations.
- Updated capability mapping for host file invocation.
- Security/resilience/current-state tests and documentation.

## Dependency Impact

- SB05 Sandbox and final live-folder proof depend on this adapter; weak root/freshness proof is a security/correctness blocker.

## Validation Depth

- `Critical security and freshness foundation`.

## Implementation Steps

1. Transfer source/tests without `*.csproj.user`/generated files.
2. Move neutral media helpers to the correct lower boundary.
3. Preserve root canonicalization/link policy and bounded enumeration.
4. Add invocation capability without OpenUri/DownloadUri host effects.
5. Add mutation-between-reads, traversal/link/race/cancellation/range tests.
6. Audit absolute path disclosure and document safe display-path policy.

## Scope Exceptions

- Production CanDoItAll filesystem/FTP/IPFS drivers remain in main; FTP/IPFS not implemented here.

## Do Not Do

- Do not use shell execute, expose arbitrary roots/tokens, cache listings, follow disallowed links, or treat display paths as authorization.

## Acceptance Checklist

- [x] Existing and new adapter/content-bridge tests pass (83/83).
- [x] Root cannot be escaped by canonical occurrence keys; reparse points are inert/excluded.
- [x] Disabled/live consumers observe mutations; the adapter contains no listing cache.
- [x] File items advertise host invocation eligibility.
- [x] No direct open/download/copy side effect.
- [x] Adapter is optional and UI-independent.

## Proof Required

- `bundle://proof/SB04/manifest.md` and `semantic-invariants.md`.
- Failing-first file-invocation/current-mutation and adversarial traversal/link cases; passing transcripts; hashes; source/security assertions; anti-stub audit; Sandbox browse smoke.

## Browser Validation Logging

- Deferred dependent smoke in SB05: browse the sandbox root, mutate/refresh, double-click file, assert host event.

## Progression Gate

- SB05 may start after AC4 and architecture/security gate Pass; its real browser flow completes dependent proof.

## C# Architecture Impact

- Isolates concrete filesystem concerns behind neutral contracts.

## Boundary Ownership

- Provider owns filesystem mapping only; hosts own access policy beyond configured root and all effects.

## Dependency Direction

- Providers.FileSystem -> Abstractions; no UI/Core/main reference unless an approved contract-only exception is documented and repaired.

## Pattern Decision

- Adapter; rejected universal storage driver and host shell facade.

## Testability Contract

- Uses temporary directories and controlled link fixtures only in adapter integration tests; pure token/path helpers have isolated tests.

## Partial Class Policy

- No partial types.

## Architecture Proof Required

- Project graph, security test transcript, source assertions, CodeAnalytics dependency refresh, gate Pass.

## Suggested Agent Prompt

```text
Implement SB04 only. Deliver a root-confined optional adapter with host-invocation metadata and no side effects/cache; treat every escape or stale-read gap as blocking.
```
