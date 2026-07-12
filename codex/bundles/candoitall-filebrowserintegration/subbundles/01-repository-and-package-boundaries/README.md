# SB01 — Repository and Package Boundaries

## Status

- `Completed`
- Closure gate passed 2026-07-11.

## Objective

Create the standalone .NET 10 solution, package policy, project graph, documentation skeleton, and test/sandbox composition boundaries before production behavior is transferred.

## Success Criteria

- Every planned project exists and the solution restores/builds.
- Abstractions has zero package/project references.
- No production reference points to CanDoItAll, Components source paths, tests, sandbox, or a concrete optional implementation in the wrong direction.

## Covered Inputs

- R001-R004, R023, R029-R030; N001-N003, N015, N017.

## Prerequisites

- Prepared bundle validator and manual readiness gate pass.

## Exact Source References

- `C:\repositories\CanDoItAll.FileTools\README.md`
- `C:\repositories\CanDoItAll.Components\Directory.Build.props`
- `C:\repositories\CanDoItAll.Components\CanDoItAll.Components.slnx`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/02-csharp-dependency-direction.md`

## Deliverables

- `CanDoItAll.FileTools.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`.
- Planned production/test/sandbox projects with correct SDK/packability/metadata/references.
- Root README with package-selection and architecture map.
- Test project/platform detection recorded.

## Dependency Impact

- Every later phase compiles against these boundaries; a wrong reference invalidates all testability and optional-dependency claims.

## Validation Depth

- `Critical foundation`.

## Implementation Steps

1. Record initial git state and preserve `.gitignore`.
2. Scaffold exact projects from the boundary map without feature stubs that pretend behavior works.
3. Add central versions and repository package metadata.
4. Add references only in allowed directions and solution folders.
5. Restore/build and run dependency/source audits.
6. Refresh FileTools CodeAnalytics baseline.

## Scope Exceptions

- No browser/interaction behavior closes in this phase.

## Do Not Do

- Do not reference sibling repository projects, create a broad Common project, publish packages, or touch main CanDoItAll.

## Acceptance Checklist

- [ ] Clean restore/build.
- [ ] Abstractions zero dependencies.
- [ ] No cycle/forbidden reference.
- [ ] Sandbox/tests non-packable; product projects packable.
- [ ] CodeAnalytics snapshot healthy.

## Proof Required

- `bundle://proof/SB01/manifest.md` and `semantic-invariants.md`.
- Failing-first reference audit for an intentionally forbidden fixture or source assertion, then passing graph proof.
- Restore/build transcripts, project hashes, CodeAnalytics inventory/dependencies, anti-stub audit, downstream Abstractions compile smoke.

## Browser Validation Logging

- N/A; no browser-visible behavior is accepted here.

## Progression Gate

- SB02 may start only when the graph builds independently, Abstractions is dependency-free, and the architecture review says Pass.

## C# Architecture Impact

- Establishes all compile-time boundaries.

## Boundary Ownership

- Exact owners are defined in `architecture/01-csharp-boundary-map.md`.

## Dependency Direction

- Must match `architecture/02-csharp-dependency-direction.md` with zero project cycle.

## Pattern Decision

- No behavioral pattern yet; composition projects and optional implementation boundaries are justified by independent dependencies/lifecycles.

## Testability Contract

- Each later owner has a direct test project; no test needs a full host for pure behavior.

## Partial Class Policy

- No production partial type is introduced in this scaffold.

## Architecture Proof Required

- Before/after reference graph, zero-dependency assertion, CodeAnalytics snapshot/health/cycles, architecture gate Pass.

## Suggested Agent Prompt

```text
Implement SB01 only. Establish and prove the exact standalone package graph; stop on any reverse/cross-repository reference or cycle.
```
