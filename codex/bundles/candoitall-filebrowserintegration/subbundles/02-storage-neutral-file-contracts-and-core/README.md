# SB02 — Storage-Neutral File Contracts and Foundations

## Status

- `Completed`
- Closure gate passed 2026-07-11.

## Objective

Move/design the BCL-only public file contracts and validation helpers that independently support FileBrowser, FileInteraction, and host adapters.

## Success Criteria

- Identity/source/item/query/page/capability/content/action/freshness and interaction/mode/profile/save/autosave/preview/history contracts compile in Abstractions.
- Contracts expose no Blazor, FileInfo, cache, main storage, or implementation type.
- Options and lifetimes have direct positive/negative tests.

## Covered Inputs

- R002-R009, R012-R019, R021; N002-N003, N006-N007, N009-N016.

## Prerequisites

- SB01 closure manifest, architecture checkpoint AC1, healthy graph.

## Exact Source References

- `repo://src/CanDoItAll.FileTools.Abstractions/FileBrowser`
- `repo://src/CanDoItAll.FileTools.Abstractions/FileInteraction`
- `bundle://proof/SB02/transcripts/source-assertions.md`
- `bundle://architecture/05-filebrowser-contract.md`
- `bundle://architecture/06-fileinteraction-design.md`

## Deliverables

- Abstractions contracts/value types and XML docs.
- Neutral media/extension normalization reusable by providers and interaction resolution.
- Explicit content lease/save payload lifetime and revision semantics.
- Contract compatibility/migration notes for transferred names.

## Dependency Impact

- SB03/SB04/SB06 consume these contracts; leaking implementation types forces reverse references and invalidates package modularity.

## Validation Depth

- `Critical foundation`.

## Implementation Steps

1. Inventory every current public type and classify contract versus runtime projection.
2. Move immutable contracts first with deliberate namespace/package names.
3. Add interaction/save/history/freshness contracts and validation.
4. Keep built-in UI action projection/runtime behaviors outside Abstractions.
5. Port/add unit and negative tests.
6. Audit package/reference/type dependencies.

## Scope Exceptions

- No session, filesystem, resolver implementation, scheduler, renderer, or main adapter.

## Do Not Do

- Do not add Microsoft.Extensions caching/DI packages, Razor, FileInfo/DirectoryInfo, Markdig, host entities, executable callbacks, or service location.

## Acceptance Checklist

- [ ] Zero project/package dependencies.
- [ ] Provider and interaction contracts cover requested use cases without CanDoItAll types.
- [ ] Invalid identities/options/ranges/revisions fail deterministically.
- [ ] Content leases dispose exactly once according to ownership.
- [ ] Public API docs explain effect authority and lifetime.

## Proof Required

- `bundle://proof/SB02/manifest.md` and `semantic-invariants.md`.
- Negative compile/source audit proving forbidden types absent; semantic positive fake provider/content/save/profile compilation tests.
- Test/build transcripts, hashes, anti-stub audit, SB03/SB04/SB06 compile spike as downstream smoke.

## Browser Validation Logging

- N/A; contracts are not browser-visible.

## Progression Gate

- SB03, SB04, and SB06 may start only after zero-dependency and direct contract tests pass and AC2 closes.

## C# Architecture Impact

- Extracts stable contracts from the former combined Core and creates interaction extension contracts.

## Boundary Ownership

- Abstractions owns shapes/lifetimes only; runtime behavior remains downstream.

## Dependency Direction

- Downstream -> Abstractions; never reverse.

## Pattern Decision

- Adapter/profile contracts enable future strategies/catalogs; no implementation pattern is embedded in DTOs.

## Testability Contract

- All validation/lifetime behavior tests instantiate contract types directly with BCL fakes.

## Partial Class Policy

- No partial types.

## Architecture Proof Required

- Type inventory, source audit, dependency graph, direct tests, architecture gate Pass.

## Suggested Agent Prompt

```text
Implement SB02 only. Move contracts before behavior and keep Abstractions demonstrably BCL-only; reopen SB01 if any reference is required.
```
