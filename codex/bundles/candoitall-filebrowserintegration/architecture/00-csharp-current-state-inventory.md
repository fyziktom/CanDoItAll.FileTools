# C# Current-State Inventory

## CodeAnalytics evidence

| Scope | Snapshot | Health | Relevant result |
| --- | --- | --- | --- |
| Components FileBrowser | `snap-20260711132114-bf6d2cf4` | 7 projects/89 docs, no blocking errors or cycle | Contract/runtime/UI concentration and exact transfer graph established |
| CanDoItAll integration | `snap-20260711132548-8a755009` | 8 projects/188 docs; warnings and one pre-existing Infrastructure module cycle | Current module-to-Infrastructure direction and integration owners established |
| CanDoItAll Infrastructure re-audit | `snap-20260711171556-e982f9a8` | 49 docs/195 types/1,093 members/37 DI registrations/4 entities; no blocking diagnostics; one pre-existing module cycle | Storage still lacks browsing operations and cache registration; native sidecar boundary confirmed |
| CanDoItAll Projects re-audit | `snap-20260711172123-af247a67` | 15 docs/58 types/302 members/4 registrations; no diagnostics/cycles | Exact Projects page/board/model anchors and dependency direction confirmed |
| FileTools before scaffold | no snapshot; correlation `code-analytics_27a9784d4fa84e1a9c4e4755fa35b2be` | Expected `SolutionPathNotFound` | Must refresh after solution creation |

Full-solution and 11-project re-audit refresh attempts were time-boxed during MSBuild loading. Future CanDoItAll execution must capture a fresh scoped graph at re-entry; the healthy narrow snapshots and exact `.csproj` reads are planning evidence, not a waiver.

## Responsibility inventory

| Current owner | Responsibilities currently combined | Target slice |
| --- | --- | --- |
| `FileBrowser.Core` | public contracts/DTOs, URI normalization, provider catalog, response validation, ordering, navigation, search strategies, tree retention, session lifecycle | Public contracts -> Abstractions; browser behavior -> FileBrowser.Core |
| `FileBrowserSession` | initialization, source switching, browsing, paging, search, selection, navigation history, cancellation, errors, retries, tree mutation, event emission | Thin facade over focused loader/search/navigation/selection/retention collaborators |
| `FileBrowserTreeStore` | page retention, container snapshots, consistency, invalidation, capacity | Explicit `IFileBrowserStateStore`; bounded and disabled implementations/policies |
| `FileBrowser.BaseLib.FileBrowser` | session subscription, initialization, search debounce, view/filter/sort, selection, menu projection, host events | UI coordinator plus extracted subscription/search/menu projection helpers; framework-native RCL |
| `FileBrowserItemActions` | UI affordances plus direct copy/open/download effects | Descriptive actions only; all effects emitted to host |
| Filesystem provider | path resolution, enumeration, paging, metadata/MIME, links, content read, actions | Optional adapter; reusable media classification moves to light contracts/core where neutral |

## Large classes and partial-class policy

- `FileBrowserSession.cs`: 1,315 lines / 89 members. It must shrink to a cohesive facade; moving it unchanged fails the architecture gate.
- `FileBrowserTreeStore.cs`: 544 lines. Storage and policy must separate so “disabled retention” is a real behavior, not a special flag hidden in the session.
- `FileBrowser.razor.cs`: 534 lines / 64 members. Razor code-behind partial use is permitted, but only for a cohesive component; non-rendering policies move to top-level types.
- Existing CanDoItAll `ProjectStructurePage` is already a broad partial cluster. Future integration adds a child window component and injected scope provider; no new responsibility is added to page partials.

## Constructor/direct-instantiation observations

- Existing FileBrowser providers/sessions are manually created; no reusable DI registration/builder exists.
- The sandbox has the only discovered FileBrowser-area registration (theme state).
- CanDoItAll creates `WorkspaceFileService` directly in many call sites, so later invalidation cannot assume DI intercepts every mutation.
- Main `IStorageDriver` supports test/save/open/delete only; it cannot implement FileTools browse contracts without a sidecar browse adapter.

## Current tests

- Core characterization: documented 156 tests across URI safety, models, catalog, provider validation, navigation, tree store, session boundaries, and search.
- Filesystem characterization: documented 47 tests across paging, metadata, links, options, security, and resilience.
- UI contract/helper characterization: documented 51 tests.
- Missing behavior tests: file invocation from filesystem items, host-only actions, disabled retention, public invalidation, density/chrome, FileInteraction resolution/history/save/preview, real browser proof.

## Current dependency hazards

- FileBrowser UI depends on unpublished `CanDoItAll.Components.BaseLib`; a source sibling reference would make FileTools non-standalone.
- Provider capability data can currently create executable anchors, bypassing the host.
- Main storage reference tokens are unsigned base64url JSON and must not become authorization tokens.
- Project `UpdatedAtUtc` and storage catalog `UpdatedAtUtc` represent entity/configuration changes, not file-set revision.
- `ProjectStructureLocalFileOpener` accepts certain existing rooted metadata paths and is an OS-open helper, not a principal-aware browse authorization service.
- Current Resources connectors have repository/folder/file/FTP but no generic storage-object/IPFS connector, which blocks complete browse-to-resource promotion for those sources.
