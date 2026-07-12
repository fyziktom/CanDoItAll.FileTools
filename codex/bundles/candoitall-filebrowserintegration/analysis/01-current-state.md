# Current State

## FileTools baseline

- The repository contains only `README.md` plus an untracked, user-owned `.gitignore`.
- There is no solution, project, source, test, sandbox, `global.json`, or package policy.
- Installed SDK is `10.0.301`; `dotnet build .` and `dotnet test .` correctly fail with `MSB1003` before scaffolding.
- CodeAnalytics cannot create a baseline snapshot until a solution exists. This is a tool-input gap, not evidence of a healthy dependency graph.

## Existing Components FileBrowser

CodeAnalytics snapshot `snap-20260711132114-bf6d2cf4` loaded seven projects, 89 documents, 158 types, 1,248 members, and reported no cycle. It also identified concentration that must not simply be renamed:

| Surface | Evidence | Consequence |
| --- | --- | --- |
| Browser orchestration | `repo://CanDoItAll.Components/src/CanDoItAll.Components.FileBrowser.Core/Runtime/FileBrowserSession.cs` — 1,315 lines | Slice lifecycle, loading, search, navigation, selection, and provider coordination into independently testable owners or a thin session facade. |
| Tree/cache | `repo://CanDoItAll.Components/src/CanDoItAll.Components.FileBrowser.Core/Runtime/FileBrowserTreeStore.cs` — 544 lines | Make cache policy explicit and allow disabled/no-reuse behavior; component cannot own integration caching. |
| UI coordinator | `repo://CanDoItAll.Components/src/CanDoItAll.Components.FileBrowser.BaseLib/Components/FileBrowser.razor.cs` — 534 lines, 64 members | Extract state/subscription/search and menu projections; preserve EventCallback flow and cancellation safety. |
| Contract/runtime coupling | Core project contains Models, provider interfaces, Search, Runtime, cache, and navigation | Move storage-neutral models/contracts to a dependency-free Abstractions project. |
| Host action leak | `FileBrowserItemActions.razor` directly renders Open/Download URIs and CopyButton behavior | All effectful open/download/copy/custom actions must flow through `ActionRequested` or `ItemInvoked`; provider data is descriptive, not executable UI authority. |
| File activation gap | Filesystem factory assigns files Select/CopyPath but not Open; tests encode that behavior | Add explicit host-invocation capability and prove pointer double-click emits the event while folders still navigate internally. |
| Cache policy gap | Session always reuses its tree store and tree options reject zero capacity | Add explicit session retention policy (`Disabled`, bounded) and invalidation/reset inputs; do not confuse it with CanDoItAll cross-request cache. |
| Compact UI gap | Existing CSS has container queries and narrow-width coverage but no explicit compact/minimal mode or low-height floating scenario | Add density/chrome modes and validate both width and height constraints. |

The existing implementation has valuable paging, provider response validation, search strategies, selection/navigation, URI safety, filesystem confinement, JS module usage, and extensive xUnit characterization tests. Those behaviors are transfer assets, not reasons to retain the current project ownership.

## Existing project dependencies

```text
FileBrowser.BaseLib -> FileBrowser.Core + CanDoItAll.Components.BaseLib
FileBrowser.Providers.FileSystem -> FileBrowser.Core
FileBrowser.Sandbox -> BaseLib + FileBrowser projects
Tests -> corresponding FileBrowser projects
```

The target repository cannot use a cross-repository source reference to Components. The transferred RCL must either be framework-native or expose an optional adapter package; its core browser and interaction projects must remain independent.

## CanDoItAll read-only baseline

CodeAnalytics snapshot `snap-20260711132548-8a755009` loaded eight scoped projects, 188 documents, 713 types, 5,236 members, 49 DI registrations, and 25 persistence entities. It found one existing module-level cycle inside Infrastructure; this bundle must not claim or attempt to repair that unrelated condition.

Relevant observed owners include:

- `repo://CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/Storage/Models/StorageModels.cs`
- `repo://CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`
- `repo://CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FtpStorageDriver.cs`
- `repo://CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/IpfsStorageDriver.cs`
- `repo://CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/Storage/Routing/DefaultStorageRoutingService.cs`
- `repo://CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.cs`
- `repo://CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `repo://CanDoItAll/src/Modules/CanDoItAll.Modules.Projects`
- `repo://CanDoItAll/src/Modules/CanDoItAll.Modules.Processes`
- `repo://CanDoItAll/src/Modules/CanDoItAll.Modules.Resources`
- `repo://CanDoItAll/src/UI/CanDoItAll.AppComponents`
- `repo://CanDoItAll/src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`

Existing project-reference direction has Modules -> Infrastructure and Composition -> Modules/Infrastructure. The future FileTools adapters should live in CanDoItAll Infrastructure/Application integration code, while modules and UI consume FileTools contracts/components through narrow facades. FileTools must never point back into those projects.

## Working-tree safety

- `CanDoItAll.FileTools`: user-owned untracked `.gitignore`; preserve it.
- `CanDoItAll.Components`: clean at baseline.
- `CanDoItAll`: pre-existing modified architecture skill files; do not touch, stage, reset, or include them in proof.

