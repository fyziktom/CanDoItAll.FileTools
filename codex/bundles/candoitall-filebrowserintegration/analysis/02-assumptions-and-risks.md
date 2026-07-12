# Assumptions And Risks

## Assumptions

- .NET 10 is the initial target because both the source FileBrowser and installed SDK use it.
- Existing xUnit characterization tests may be transferred before choosing any later test-framework migration; framework churn is not part of this task.
- A file occurrence is identified by provider/source plus provider-local key; paths and URIs are presentation metadata, not universal identity.
- The host treats successful completion of `SaveRequested` as persistence acknowledgement; an exception represents failure/conflict.
- Basic built-ins mean text, Markdown, image, PDF/browser-object presentation, and a Mermaid extension seam. Office/CSV/XLSX editors and diff UI remain follow-up packages.
- FileTools session retention is bounded UI state; CanDoItAll HybridCache is a separate integration decorator for expensive aggregate listings.
- The first CanDoItAll cache delivery is memory-primary/process-local: its catalog revision/change sink is in memory and resets with the cache on restart. Durable/shared revision is deferred until a distributed cache secondary is deliberately introduced.
- FileTools item identity and `StorageJson` reference encoding are descriptive transport data, never authorization credentials. CanDoItAll issues bounded opaque server handles only after principal-aware scope authorization.
- Missing storage browse-cache configuration is interpreted as Disabled and typed settings are serialized backward-compatibly in `StorageCatalogRecord.ConfigJson`, not `MetadataJson` or a new table.

## Critical Path Risks

- If Abstractions retains runtime or Blazor types, all later packages inherit unnecessary dependencies and the primary request fails.
- If the FileBrowser runtime is copied as one 1,315-line owner, FileInteraction and future providers will grow the same monolith.
- If source capabilities still translate directly into anchor actions, remote/provider content can bypass host authorization and viewer selection.
- If cache policy cannot be disabled, process-run and agent-working folders can present stale state.
- If renderer selection and persistence acknowledgement are not separated, editing cannot be extended safely.
- If the sandbox is not truly standalone, FileTools portability is unproven.
- If `ProjectStructureLocalFileOpener` or file existence is reused as browse authorization, arbitrary absolute node metadata can escape managed workspace/storage scope.
- If authorization scope/runtime profile is absent from a filtered-listing cache key, one principal or database profile can receive another's entries. Shared raw cache entries are safe only when every hit is reauthorized before mapping/handle issuance.
- If a distributed HybridCache secondary is enabled while revisions remain process-local, nodes can select obsolete distributed listings after another node changes the source.
- Projects filters currently live inside `ProjectsBoard`; wrapping the board itself in a Projects/Files tab would make the Files source-set controls disappear.
- Resources currently has no IPFS or generic storage-object connector, so browsing alone cannot complete Add as resource for those items.
- Scope-provider ownership can create reverse references: Projects must not reference Workbench/Resources, Resources must not reference Workbench, and Processes must not reference Workbench-owned process policy.

## Validation Risks

- Microsoft Learn MCP and Components MCP are unavailable; authoritative web documentation and direct source inspection are substituted and recorded.
- Browser proof requires a running sandbox and Playwright. If browser tooling is unavailable, UI subbundles remain open rather than being closed from component tests alone.
- Low-height floating windows can fail even when 390px phone width succeeds; validation must include height-constrained viewports.
- PDF object URLs and JS disposal differ between WebAssembly and server circuits; tests plus browser proof must cover module disposal and graceful disconnect behavior where implemented.
- The CanDoItAll repo is actively refactoring, so exact integration source paths can become stale. The plan must include a re-entry inventory/snapshot gate.
- Workbench and Processes are the highest-volatility integration areas (13 and 20 recent module commits respectively in the two-week re-audit window; Processes core had 15). Fresh exact anchors and scoped graph proof are mandatory immediately before those phases.
- Cross-repository deletion from Components must happen only after FileTools build/test/browser proof and a source manifest comparison.

## Reopen Triggers

- Any FileTools project references CanDoItAll Infrastructure, persistence, modules, or a concrete storage SDK.
- Abstractions gains a NuGet package, Razor SDK, `FileInfo`, cache, or provider implementation dependency.
- A future renderer/provider addition still requires modifying the FileBrowser session or a giant type switch.
- File double-click does not emit a host event, or file effects occur through direct anchors without host approval.
- Disabled retention still returns stale results after the source changes.
- Compact mode clips breadcrumbs, toolbar actions, context menus, status, or selected items.
- Autosave marks content clean before the host handler completes successfully.
- Undo/redo survives across a file/revision boundary incorrectly.
- Post-transfer CodeAnalytics shows a cycle or unexpected reverse reference.
- FileTools proof fails after Components source removal; reopen transfer before continuing.
- Fresh CanDoItAll re-entry analysis contradicts an integration path; update the plan rather than applying stale instructions.
- Any browse/open/edit/save route accepts an unsigned storage reference token, FileTools identity, rooted path, or existence check as authority instead of resolving a principal-bound opaque server handle.
- Any authorization-filtered cache omits an authorization-scope fingerprint, or a raw shared cache returns data without reauthorization.
- `StorageCatalogRecord.UpdatedAtUtc` or `Project.UpdatedAtUtc` is used as the mixed file-catalog revision.
- A distributed cache secondary is enabled before durable/shared revision and cross-node profile/invalidation proof exists.
- Provider cache settings are hidden in `MetadataJson`, missing settings enable caching, or immutable policy can be selected without provider proof.
- Projects/Files tabs hide the filters that define `FilteredProjectSummaries`, or the source-set fingerprint omits ordered project ids, hierarchy/include-subprojects state, or catalog revision.
- Workbench adds another duplicated `browse-files` switch branch rather than a focused coordinator/resolver, or trusts arbitrary node metadata roots.
- Resources promotion persists browser display metadata/opaque handles directly or closes without a `resource.storage-object`/IPFS connector.
