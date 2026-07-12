# Structured Input

## Objectives

1. Establish FileTools as the domain owner for reusable file models, provider contracts, browser runtime, file browsing UI, file interaction UI, and optional adapters.
2. Transfer the existing FileBrowser from Components without retaining its current architectural concentration or host-action leaks.
3. Support project, subproject, folder/artifact, process-run, resources, IPFS, filesystem, and FTP browsing through storage-neutral provider contracts.
4. Add an extensible View/Edit interaction shell with host-owned persistence, autosave policies, preview debounce, provider-specific undo/redo, and future diff seams.
5. Validate normal, compact/floating, and narrow layouts in a standalone sandbox.
6. Prepare exact future integration steps for CanDoItAll while leaving that repository untouched.

## Non-negotiable constraints

- FileTools does not reference CanDoItAll projects or storage-driver models.
- Abstractions contains no Blazor, filesystem, cache, persistence, provider SDK, Office, FTP, IPFS, or editor package dependency.
- FileBrowser invokes host events for files and host actions; it does not decide how the app opens or persists a file.
- Caching is a source/adapter concern, never a visual component concern.
- Concrete file-type renderers are registered explicitly so consumers pay only for selected packages.
- Global script and stylesheet conflicts are prevented through RCL CSS isolation and collocated JS modules.
- CanDoItAll is read-only in this run.

## User-visible scenarios

- Project browser cards/list can switch to a combined file view for filtered projects.
- A project card can open a project-scoped file browser dialog.
- Project structure canvas can open a floating browser with an include-subprojects choice.
- Folder nodes and process-run history can open current, uncached file views in floating windows.
- Resources can browse and select project, IPFS, filesystem, and other attached sources.
- Remote providers such as FTP use the same file source boundary.
- Compact/floating surfaces remain useful at low width and height.
- File double-click emits an event; the host decides the real viewer/action.
- FileInteraction selects a registered viewer/editor based on type and mode and can split edit/preview with bounded refresh.

## Deferred outcomes

- Actual CanDoItAll module edits, database migrations, project file references, toolbar/card buttons, and production cache wiring.
- Full DOCX/XLSX/CSV/media editing and a production diff tool.
- Persisted/distributed cache deployment; the future design must permit it via HybridCache/IDistributedCache.

