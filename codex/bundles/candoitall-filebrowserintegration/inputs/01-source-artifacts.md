# Source Artifacts

## Raw inputs

- `bundle://inputs/00-original-request.md` — verbatim Markdown request copied from `C:/programovani/candoitall-filebrowserintegration/prompt.md`.
- `bundle://inputs/00-original-request.txt` — verbatim text copy supplied beside the Markdown request.

## Repositories

- `repo://CanDoItAll.FileTools` — `C:/repositories/CanDoItAll.FileTools`, commit `5c7110c`, branch `main` at baseline.
- `repo://CanDoItAll.Components` — `C:/repositories/CanDoItAll.Components`, branch `file-explorer` at baseline.
- `repo://CanDoItAll` — `C:/repositories/CanDoItAll`, branch `memory-providers` at baseline; read-only and already dirty with unrelated skill edits.

## Architecture evidence

- CodeAnalytics snapshot `snap-20260711132114-bf6d2cf4` for the seven Components FileBrowser projects/tests/sandbox.
- CodeAnalytics snapshot `snap-20260711132548-8a755009` for Infrastructure, Projects, Processes, Resources, AppComponents, Composition, Process Persistence, and Process Projections.
- Re-audit Infrastructure snapshot `snap-20260711171556-e982f9a8`: 49 documents, 195 types, 1,093 members, 37 DI registrations, 4 entities, no blocking diagnostics, and one pre-existing Infrastructure module-level cycle.
- Re-audit Projects snapshot `snap-20260711172123-af247a67`: 15 documents, 58 types, 302 members, 4 registrations, and no diagnostics or cycles.
- Full-solution and 11-project re-audit refresh attempts were time-boxed because MSBuild loading did not complete promptly. The two healthy narrow snapshots, the usable cross-project snapshot, direct current `.csproj` reads, and exact source inspection are the recorded fallback; a fresh scoped graph is required at future CanDoItAll implementation re-entry.
- FileTools pre-scaffold CodeAnalytics failure correlation `code-analytics_27a9784d4fa84e1a9c4e4755fa35b2be` (`SolutionPathNotFound` because the repository contained no project).

## CanDoItAll re-audit anchor corrections

- Projects board is `repo://CanDoItAll/src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectsBoard.razor`, not `.../Components/ProjectsBoard.razor`.
- Workbench toolbar is `repo://CanDoItAll/src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureToolbarActions.razor`, not `.../Components/ProjectStructureToolbarActions.razor`.
- Projects UI evidence also includes `Pages/ProjectsPage.razor`, `Pages/Components/ProjectModalHost.razor`, and `ProjectModels.cs`.
- Workbench security/action evidence includes `ProjectStructureLocalFileOpener.cs`, `ProjectStructureNodeActionCapabilityResolver.cs`, `CanvasAdapters/ProjectStructureActionCatalogAdapter.cs`, `ProjectStructureMenuComposition.cs`, `Pages/ProjectStructurePage.NodeQuickActions.cs`, and `Pages/ProjectStructurePage.NodeEditing.cs`.
- Process evidence includes `Modules/CanDoItAll.Modules.Processes/Pages/LiveProcessesPage.razor`, `Components/LiveProcessesDashboard.razor`, `Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`, and `Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs`.
- Resources evidence includes `Pages/ResourcesPage.razor`, its code-behind, `ResourceModels.cs`, `ResourceConnectorPlugins.cs`, and `ResourceSourceSnapshotProvider.cs`.
- Storage evidence includes `Storage/Abstractions/StorageContracts.cs`, `Storage/Models/StorageModels.cs`, `Storage/Persistence/StoragePersistenceModels.cs`, `Storage/StorageJson.cs`, and `DependencyInjection/InfrastructureServiceCollectionExtensions.cs`.

The re-audit was read-only. The main repository remained on `memory-providers` with the same 11 pre-existing modified skill files before and after inspection; no source-repository file was authored by this bundle correction.

## Microsoft guidance

- `https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0`
- `https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0`
- `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/css-isolation?view=aspnetcore-10.0`
- `https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/location-of-javascript?view=aspnetcore-10.0`
- `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0`
- `https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo?view=net-10.0`
- `https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/using-async-for-file-access`

## Optional renderer guidance

- `https://www.nuget.org/packages/Markdig/1.3.2` — current optional Markdown package/version and supported target frameworks.
- `https://github.com/xoofx/markdig` — primary Markdig usage and extension documentation.
- `https://github.com/xoofx/markdig/blob/7964bd0160d4c18e4155127a4c863d61ebd8944a/src/Markdig/MarkdownExtensions.cs` — primary-source `DisableHtml` behavior used by the safe default Markdown pipeline.
- `https://securitylab.github.com/advisories/GHSL-2024-016_NuGetGallery/` — concrete evidence that disabling raw HTML alone does not make Markdig-generated links safe; dangerous URL schemes must also be filtered.

## Tool availability record

- Microsoft Learn MCP requested by the user: not present in the exposed tool catalog; official Microsoft Learn web content used as the fallback.
- CanDoItAll Components MCP required by local skill policy: not present in the exposed tool catalog; exact component source and sandbox usage were inspected directly.
