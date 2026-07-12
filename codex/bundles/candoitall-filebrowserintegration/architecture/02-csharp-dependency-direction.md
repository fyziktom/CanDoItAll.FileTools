# C# Dependency Direction

## Allowed directions

```text
Sandbox/host composition
  -> optional renderer adapters
  -> FileBrowser.Components / FileInteraction.Components
  -> FileBrowser.Core / FileInteraction.Core
  -> Abstractions

Providers.FileSystem -> Abstractions
Future CanDoItAll adapters -> FileTools Abstractions/Core
CanDoItAll modules/UI -> main integration abstractions + FileTools components
```

For the future main integration, keep Infrastructure browse contracts native and FileTools-free. The small `CanDoItAll.FileTools.Integration.Abstractions` project points to neutral FileTools contracts; the outer `CanDoItAll.FileTools.Integration` adapter points to Integration.Abstractions, FileTools, and Infrastructure. Module-owned implementations sit behind Integration.Abstractions: Workbench owns project/node scopes, Processes owns run scopes, Resources owns source catalog/promotion, and Composition wires them.

## Forbidden references

- Abstractions -> any implementation, UI, cache, filesystem, provider SDK, or main-domain project.
- Core -> Components, FileSystem, Markdig, CanDoItAll Infrastructure/modules/persistence.
- Components -> FileSystem, CanDoItAll storage, or optional Markdown/Mermaid/Office implementations.
- FileSystem -> Components/Core runtime unless a contract utility cannot be moved to Abstractions; the desired final reference is Abstractions only.
- FileTools -> `C:/repositories/CanDoItAll` source projects.
- Projects -> Workbench in the future main integration; Workbench already references Projects and the reverse edge would cycle.
- Projects -> Resources; Resources already references Projects and the reverse edge would cycle.
- Resources -> Workbench; Workbench already references Resources.
- Processes or Processes.Application -> Workbench. Neutral run-root policy belongs in Processes.Application/a small process integration core and may be consumed by Workbench.
- CanDoItAll Infrastructure -> an integration implementation that itself references Infrastructure. Put that sidecar above Infrastructure and wire it in Composition.
- CanDoItAll Infrastructure -> FileTools. Native `IStorageBrowseDriver` sidecars and DTOs remain in Infrastructure; only the outer adapter maps them.
- CanDoItAll modules -> the outer integration implementation for service location. Modules consume Integration.Abstractions; Composition registers implementations.

## Current-to-target reference change

| Current | Target |
| --- | --- |
| BaseLib UI -> FileBrowser.Core + Components.BaseLib | FileBrowser.Components -> FileBrowser.Core + Abstractions + ASP.NET shared framework |
| FileSystem -> combined FileBrowser.Core | FileSystem -> Abstractions |
| Core has no refs but contains contracts and runtime | Abstractions has no refs; Core -> Abstractions |
| Sandbox -> sibling Components.BaseLib and FileBrowser projects | Sandbox -> FileTools projects only |

## Cycle handling

If a move creates a cycle, stop and move only the shared record/interface into Abstractions. Do not create `Common`, use service location, or reference a renderer from core.

## Package policy

- `net10.0` initial target, nullable and implicit usings enabled, deterministic builds.
- Central package management at repository root.
- Product libraries packable; tests/sandbox not packable.
- Abstractions zero package references.
- Optional renderer dependencies stay in optional packages.
- No sibling-repository path in a NuGet package or build target.

## Required proof

- before/after `.csproj` reference table;
- `dotnet list ... reference` or solution inventory transcript;
- CodeAnalytics solution inventory/dependencies after implementation;
- zero project-level cycles;
- source audit for forbidden namespaces/package references;
- clean standalone restore with the Components repository not used as a source dependency.
