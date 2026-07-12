# Normalized Requirements

| ID | Requirement | Observable acceptance |
| --- | --- | --- |
| R001 | FileTools is the owner of file-domain contracts, browser runtime/UI, interaction runtime/UI, and optional file adapters. | Standalone solution/package graph exists; Components no longer owns FileBrowser production projects after transfer proof. |
| R002 | FileTools must not depend on CanDoItAll application/storage/persistence projects. | Project graph/source audit contains no such reference or namespace. |
| R003 | A small BCL-only Abstractions package must isolate models, enums, capabilities, provider contracts, content, save, preview, and history contracts. | Package has zero NuGet/project references and no Razor/filesystem/cache types. |
| R004 | Consumers can select only Abstractions/Core/FileSystem/Components or optional renderer packages. | Package references are one-way; Components/Core do not reference FileSystem or optional renderers. |
| R005 | FileBrowser accepts one or more provider-neutral sources and supports project/subproject aggregation without knowing CanDoItAll storage drivers. | Source/provider catalog and stable source+item identity tests pass. |
| R006 | Folder activation navigates internally while file double-click/keyboard/primary activation raises a host event. | Component tests and browser action assertions distinguish folder and file behavior. |
| R007 | Open, download, copy, custom, edit, and destructive file effects are host-owned. | No direct provider URI execution path bypasses `ItemInvoked`/`ActionRequested`; negative source assertion passes. |
| R008 | Browser session retention can be bounded or disabled and can be explicitly invalidated/refreshed. | Mutation-between-browse tests prove disabled mode reads current state and invalidation discards retained pages. |
| R009 | Cross-request/aggregate caching remains outside FileBrowser and can be added as a provider decorator. | FileBrowser packages contain no `IMemoryCache`/`HybridCache`; integration architecture locates the decorator in CanDoItAll. |
| R010 | FileBrowser exposes normal, compact, and minimal chrome/density suitable for floating windows. | Sandbox/browser proof at large desktop, 720x520, 560x360, and narrow/mobile viewports has no clipping/layering defects. |
| R011 | Loading, empty, error/retry, partial, paged, selection, list/cards, search, and multi-source states remain supported. | Transferred characterization tests and sandbox scenario assertions pass. |
| R012 | FileInteraction selects a handler deterministically by media type, extension, mode, priority, and capabilities. | Positive/negative/ambiguity resolver tests pass; unsupported state is explicit. |
| R013 | FileInteraction has View and Edit modes and is ready for future Diff mode without implementing a full diff engine now. | Public mode/capability contracts and unsupported-mode behavior exist; no closed switch blocks new modes. |
| R014 | View/edit implementations are registered by consumers so image-only apps avoid Markdown/Mermaid/Office dependencies. | Renderer registrations are explicit and optional packages are not referenced by the shell. |
| R015 | Editing requests persistence through an awaited host event; FileTools never writes through a CanDoItAll driver. | Save-request component tests prove successful handler completion marks clean and failures retain dirty/error state. |
| R016 | Manual and automatic save strategies support idle delay, fixed interval, edit-count, and cumulative changed-text-unit triggers with validated settings and dynamic host-save availability. | Deterministic scheduler tests cover trigger thresholds, cancellation, coalescing, disable/unavailable-to-available transitions, and file-switch behavior. |
| R017 | File-type-specific history providers can enable/disable undo and redo; a bounded text history implementation is included. | History catalog/state/branching/limit/file-revision negative tests pass. |
| R018 | Live preview can render beside editing and is debounced per interaction profile. | Rapid edits coalesce into one preview update; stale preview completion cannot replace a newer revision. |
| R019 | FileInteraction exposes a future diff extension point without pulling a diff dependency into base packages. | Contract/registration seam exists and unsupported Diff is explicit. |
| R020 | Basic lightweight viewing covers text, a conservative exact set of raster images, opt-in browser PDF, and Markdown; SVG/unknown formats remain inert by default, and Mermaid integration is pluggable without making the shell depend on main CanDoItAll. | Registered handlers resolve and render in sandbox; hostile SVG and unsupported image media do not embed content; optional dependencies stay isolated. |
| R021 | Component and renderer CSS/JS do not pollute the host. | CSS isolation/collocated module paths are used; no required global `window.*`, body class, or script tag is introduced. |
| R022 | The example filesystem provider is root-confined, cancellation-aware, range-readable, and resilient to traversal, links, races, and inaccessible entries. | Security/resilience tests pass on supported host semantics. |
| R023 | FileTools has a standalone sandbox and complete package/service-registration documentation. | A clean clone can restore/build/test/run without sibling source repositories. |
| R024 | CanDoItAll remains untouched in this run. | `git status` before/after shows no new main-repo changes from this task. |
| R025 | Future CanDoItAll integration covers project browser tab, project-card dialog, project-structure floating window/include-subprojects, folder nodes, process-run history, resources, IPFS/filesystem/FTP. | Each surface has an adapter, source composition, UI insertion point, cache policy, action routing, and validation step in the integration plan. |
| R026 | CanDoItAll cached listings use optional .NET HybridCache with in-memory primary now and optional `IDistributedCache` secondary later. | Architecture defines per-binding enable/disable, bounded keys/TTL/tags, stampede protection, invalidation, and multi-node caveat. |
| R027 | Filesystem/process-run agent-working folders are uncached by default; project/IPFS aggregate listings may be cached. | Cache policy matrix is explicit and integration tests are planned for each class. |
| R028 | Project aggregate invalidation represents changes across filesystem, IPFS, and subprojects, not only a folder timestamp. | Revision/change-token design has producer, consumer, lifecycle, and negative-test plan. |
| R029 | The architecture is phased with dependency, testability, partial-class, and progression gates. | Prepared/completed bundle validators and C# architecture review gates pass. |
| R030 | Transfer cleanup in Components cannot precede FileTools proof. | Components removal is the final action of the validated transfer phase and has a rollback/source-manifest check. |

## Literal-scope notes

- “All resources” is preserved as a multi-source contract, not narrowed to local files. Production adapters are deferred but every named source class has an integration row.
- “Fully ready” means FileTools implementation, tests, sandbox/browser proof, packaging boundaries, and integration design are closed. It does not mean CanDoItAll module implementation, which the user explicitly deferred.
- “Basic types” is implemented as a lightweight built-in set plus optional registrations; Office-class types are not silently claimed as complete.
