# SB04 Proof Manifest

Status: **Pass with documented residual risk** (2026-07-11).

- Semantic contract: `bundle://proof/SB04/semantic-invariants.md`.
- Failing-first/adversarial negative proof: `bundle://proof/SB04/transcripts/failing-first.md`.
- Passing semantic positive proof: `bundle://proof/SB04/transcripts/closure-summary.md`.
- Anti-stub audit: `bundle://proof/SB04/transcripts/closure-summary.md`.
- Portable source anchor: `repo://src/CanDoItAll.FileTools.Providers.FileSystem/FileSystemFileBrowserProvider.cs`.

## Commands and results

- Focused Release build: 0 warnings, 0 errors.
- Focused Release test after the interaction-content bridge: 83 passed, 0 failed, 0 skipped.
- `dotnet format --verify-no-changes` for product and test projects: exit 0.
- Final focused CodeAnalytics snapshot `snap-20260711184248-7131c945`: provider scope inspected after the content bridge; direct dependency and cycle proof remains Providers.FileSystem -> Abstractions only.
- Root repeated the focused test after final root-error/display-name hardening: 67/67 passed. The later read-only FileInteraction bridge added 16 focused cases and passed 83/83.

## Implemented surface

- Shallow, paged, always-current browse and bounded range reads.
- Canonical root-relative occurrence keys and display paths.
- Inert or excluded reparse points; reparse root rejected.
- File `Open` capability is descriptive host eligibility only; URIs are null and the provider does not implement `IFileBrowserActionProvider`.
- Safe provider errors, hardened continuation tokens, mutation/stale-cursor detection, cancellation, and owned stream disposal.
- The provider also implements `IFileContentSource` through the same root-confined reader. The host mints a `FileReference` from an already authorized source and canonical occurrence key; interaction reads do not depend on browser-session lifetime.
- Mutable filesystem reads intentionally return no optimistic-concurrency revision and expose no save target, action executor, cache, or ambient OS effect.

## Source inventory and SHA-256

```text
90db7809f1abcbb07d487c18aceaf389f5662213fe33272d994d8ab3056dd3f4  FileSystemContinuationTokenCodec.cs
dc55b69899dc2c62086df9396d3f9dba088fb063f9411713186aabce51656b25  FileSystemFileBrowserItemComparer.cs
9943c5405e7503c8bb30c964553729d58df68166442d3b19eea7f5339acef096  FileSystemFileBrowserOptions.cs
b9c27b07924763afaeca747a2d0a243334a65c4c4cd55d30dc8609ff68a0f6d4  FileSystemItemFactory.cs
34d4cfedbaa93caf1fd6a672949430b9e20040ca1f06d59d483aacf34355484c  FileSystemPathResolver.cs
c7c9d15a6c1020f1d4a505e976df55fc7fc3298324f3748f8a0f81e9c7120c26  FileSystemProviderErrors.cs
48ba498978741b6e8383cf7cde802fc3da8094716994540c4a3c3f4479704ef8  FileSystemRangeReadStream.cs
cde7f3cf483d9f9973a524be47b2d841ca1b4324f643f86175a7343d940febe6  FileSystemContentReader.cs
6320ec27e5d5a9ffbf817c7bf807ae4bd91f0800462a8b5c9cc0dc578c0b6890  FileSystemFileBrowserProvider.cs
```

The complete product/test hash listing is captured in the initiative execution notes; the hashes above are the production behavior artifacts.

## Residual risk

Portable path-based APIs cannot eliminate a malicious concurrent reparse-point swap with the same strength as OS-specific handle-relative/no-follow primitives. Stable links and observed swaps are rejected before/after opening, but this simplified adapter is for a trusted local root. A hostile-root production adapter is a separate platform-specific package/phase.

## Dependent proof

The live folder browse -> mutation -> refresh -> file activation browser smoke is owned by SB05 and is not borrowed by this core gate.
