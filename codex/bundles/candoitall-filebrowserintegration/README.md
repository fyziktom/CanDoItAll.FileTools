# CanDoItAll FileTools and FileBrowser Integration

This initiative bundle is the durable architecture, implementation, proof, and future-integration package for extracting file tooling from `CanDoItAll.Components`, establishing the standalone `CanDoItAll.FileTools` repository, and preparing (but not executing) integration into `CanDoItAll`.

## Profile

- `initiative`

## Mission

Deliver a lightweight, layered FileTools product whose storage-neutral contracts, browser runtime, safe filesystem example, responsive FileBrowser, and extensible FileInteraction shell can be consumed independently. Prove the result in tests and a real sandbox, remove FileBrowser ownership from `CanDoItAll.Components`, and leave a source-anchored implementation plan for the actively-refactoring `CanDoItAll` application.

## Outcome Contract

- Requested outcome: FileTools and FileBrowser are implementation-ready and validated; the CanDoItAll integration architecture covers projects, project structure, process runs, resources, local/IPFS/FTP sources, caching, invalidation, floating windows, and host-owned file actions.
- Hard constraints: no write in `C:/repositories/CanDoItAll`; no FileTools reference to CanDoItAll Infrastructure/storage/persistence; contracts stay BCL-only; concrete adapters remain optional; file opening and persistence remain host-owned; no global file-type JS/CSS pollution.
- Evidence required before closure: clean restore/build/test, component contract tests, safe-filesystem negative tests, CodeAnalytics before/after dependency evidence, maximized and compact/floating responsive browser proof, screenshot review, proof manifests for critical subbundles, and completed bundle validators.
- Scope exception: implementation in CanDoItAll modules is explicitly deferred because that repository is under parallel refactoring. The bundle must make that later implementation actionable without pretending it has shipped.
- Tool gap: Microsoft Learn MCP and Components MCP were not exposed in this session. Official .NET 10 Microsoft Learn pages are the authoritative fallback and the MCP absence is recorded rather than hidden.

## Repository Boundaries

| Repository | Permission in this run | Intended result |
| --- | --- | --- |
| `repo://CanDoItAll.FileTools` | Read/write | New standalone product, tests, sandbox, documentation |
| `repo://CanDoItAll.Components` | Read/write only for transfer cleanup | FileBrowser projects removed after FileTools proof; simple generic wrappers such as Mermaid remain |
| `repo://CanDoItAll` | Read-only | Current-state evidence and a future integration plan only |
| `bundle://` | Read/write | Architecture, traceability, transcripts, screenshots, gates, and closure |

## Recommended Execution Order

1. `subbundles/01-repository-and-package-boundaries`
2. `subbundles/02-storage-neutral-file-contracts-and-core`
3. `subbundles/03-filebrowser-runtime-transfer-and-responsibility-slicing`
4. `subbundles/04-filesystem-adapter-and-safety`
5. `subbundles/05-responsive-filebrowser-component-and-sandbox`
6. `subbundles/06-fileinteraction-extension-framework`
7. `subbundles/07-basic-viewers-editing-workflow-and-history`
8. `subbundles/08-validation-packaging-and-candoitall-integration-design`

See `plan/01-phase-plan.md` for the dependency graph and reopen rules.

## Durable Evidence Anchors

- Raw request: `bundle://inputs/00-original-request.md`
- Requirements: `bundle://requirements/01-normalized-requirements.md`
- Current state: `bundle://analysis/01-current-state.md`
- Target boundaries: `bundle://architecture/01-csharp-boundary-map.md`
- CanDoItAll plan: `bundle://architecture/07-candoitall-integration.md`
- Execution evidence: `bundle://reviews/01-execution-report.md`

## Validation Summary

- Bundle preparation status: `Ready — automated and manual readiness gates passed 2026-07-11`
- Execution status: `Completed — SB01-SB08 implementation/proof closed 2026-07-11; CanDoItAll production integration remains the explicitly deferred follow-up`
- Components CodeAnalytics baseline: `snap-20260711132114-bf6d2cf4` (healthy, seven scoped projects, no project cycles)
- CanDoItAll CodeAnalytics baseline: `snap-20260711132548-8a755009` (eight scoped projects; one existing Infrastructure module-level cycle; main repo remains read-only)
- FileTools CodeAnalytics baseline: unavailable before scaffolding because no solution/project existed; correlation `code-analytics_27a9784d4fa84e1a9c4e4755fa35b2be`
- Final FileTools CodeAnalytics: `snap-20260711202431-5dbb5110` (7 products, 266 types, 1,940 members, 743 edges, zero cycles/open questions/blocking errors; generated-type and four cohesive-file warnings reviewed)
- Subbundle gate review: `SB01-SB08 Pass; exact proof manifests and semantic contracts under bundle://proof`
- Final closure gate: `Pass — 433/433 tests, Release 0 warnings/errors, format clean, 7+7 FileTools packages, guarded Components cleanup, read-only main proof, C# gate, red-team, and completed validator`
- Browser validation analytics: `SB05 FileBrowser and SB07 FileInteraction matrices pass desktop/floating/narrow viewports; final console checks have zero errors/warnings; page-error counter limitation is explicit`
