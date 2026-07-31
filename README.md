# CanDoItAll.FileTools

[![CI](https://github.com/fyziktom/CanDoItAll.FileTools/actions/workflows/ci.yml/badge.svg?branch=main&event=push)](https://github.com/fyziktom/CanDoItAll.FileTools/actions/workflows/ci.yml)
[![FileBrowser.Components version](https://img.shields.io/nuget/v/CanDoItAll.FileTools.FileBrowser.Components.svg?logo=nuget&label=FileBrowser)](https://www.nuget.org/packages/CanDoItAll.FileTools.FileBrowser.Components)
[![FileBrowser.Components downloads](https://img.shields.io/nuget/dt/CanDoItAll.FileTools.FileBrowser.Components.svg?logo=nuget&label=FileBrowser%20downloads)](https://www.nuget.org/packages/CanDoItAll.FileTools.FileBrowser.Components)
[![FileInteraction.Components version](https://img.shields.io/nuget/v/CanDoItAll.FileTools.FileInteraction.Components.svg?logo=nuget&label=FileInteraction)](https://www.nuget.org/packages/CanDoItAll.FileTools.FileInteraction.Components)
[![FileInteraction.Components downloads](https://img.shields.io/nuget/dt/CanDoItAll.FileTools.FileInteraction.Components.svg?logo=nuget&label=FileInteraction%20downloads)](https://www.nuget.org/packages/CanDoItAll.FileTools.FileInteraction.Components)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/LICENSE)

Storage-neutral .NET and Blazor building blocks for browsing, viewing, and editing file-like resources. The repository targets .NET 10 and keeps application storage drivers, authorization, application-triggered navigation, downloads, clipboard access, and persistence in the host. Browser-native viewer behavior, notably embedded PDF actions, remains browser-owned and is documented as an explicit boundary.

## Ownership

This repository owns the FileBrowser and FileInteraction contracts, runtime coordination,
Blazor components, optional filesystem and Markdown adapters, and the host-side desktop
launching boundary.

It does not own application authorization, persistence, remote storage integrations,
browser behavior, or application deployment. Hosts retain those responsibilities.
`CanDoItAll.Components` continues to own unrelated general-purpose components.

## Choose packages

| Need | Package | Direct dependencies |
| --- | --- | --- |
| Shared browser and interaction contracts only | `CanDoItAll.FileTools.Abstractions` | BCL only |
| Host-side file and folder launching | `CanDoItAll.FileTools.Desktop` | BCL only |
| Provider-neutral browsing, navigation, search, paging, retention, and invalidation | `CanDoItAll.FileTools.FileBrowser.Core` | Abstractions |
| Responsive Blazor browser in Standard, Compact, or Minimal mode | `CanDoItAll.FileTools.FileBrowser.Components` | Abstractions, FileBrowser.Core, ASP.NET Core shared framework |
| Root-confined local folder browsing plus Browser and Interaction range reads | `CanDoItAll.FileTools.Providers.FileSystem` | Abstractions |
| Profile resolution, save/autosave, preview, and bounded edit history | `CanDoItAll.FileTools.FileInteraction.Core` | Abstractions |
| Blazor interaction shell plus text, raster-image, browser-native PDF, sandboxed SVG, and sandboxed fallback renderers | `CanDoItAll.FileTools.FileInteraction.Components` | Abstractions, FileInteraction.Core, ASP.NET Core shared framework |
| Optional Markdown view/edit/preview renderer | `CanDoItAll.FileTools.FileInteraction.Markdown` | Abstractions, FileInteraction.Core, FileInteraction.Components, Markdig, ASP.NET Core shared framework |

Install only the right-hand packages needed by the host. In particular, a browser-only application does not acquire Markdig or interaction renderers, and a custom provider can depend on Abstractions without taking Blazor.

## Requirements

- .NET SDK 10.0.302, pinned by `global.json` with latest-patch roll-forward
- Windows PowerShell 5.1 or PowerShell 7 for packaging and package validation

## Build and test

Run from the repository root:

```powershell
dotnet restore .\CanDoItAll.FileTools.slnx --configfile .\NuGet.config
dotnet build .\CanDoItAll.FileTools.slnx --configuration Release --no-restore
dotnet test .\CanDoItAll.FileTools.slnx --configuration Release --no-build
```

Run the maintained Sandbox:

```powershell
dotnet run --project .\samples\CanDoItAll.FileTools.Sandbox\CanDoItAll.FileTools.Sandbox.csproj --configuration Release --no-build --no-restore
```

## Minimal FileBrowser

Create a provider and session in the host's scope, then dispose the session with that scope:

```csharp
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Providers.FileSystem;

var provider = new FileSystemFileBrowserProvider(
    new FileSystemFileBrowserOptions(
        new FileBrowserSourceId("workspace"),
        configuredRootPath,
        displayName: "Workspace"));

IFileBrowserSession session = new FileBrowserSession(
    [provider],
    new FileBrowserSessionOptions(
        pageSize: 50,
        retentionMode: FileBrowserStateRetentionMode.Disabled));
```

Render the session in Razor. Files and non-navigable items notify the host; folders navigate inside the session:

```razor
@using CanDoItAll.FileTools.FileBrowser.Components

<FileBrowser Session="@session"
             DisplayMode="FileBrowserDisplayMode.Compact"
             ItemInvoked="OpenAuthorizedFileAsync"
             ActionRequested="HandleAuthorizedActionAsync" />
```

`ItemInvoked` and `ActionRequested` are notifications, not authorization. Re-resolve and authorize the item in the host immediately before any open, download, copy, navigation, or storage action. The component does not execute provider URIs or platform effects.

## Minimal FileInteraction

Compose only the renderers the application needs. Markdown is opt-in:

```csharp
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;

builder.Services.AddFileInteractionComponents(components =>
    components
        .AddBuiltIns()
        .AddMarkdown());
```

Pass an opaque, host-authorized reference and a content source to the shell:

```razor
@using CanDoItAll.FileTools.FileInteraction.Components
@inject FileInteractionComponentComposition InteractionComposition

<FileInteraction Request="@request"
                 ContentSource="@contentSource"
                 Composition="@InteractionComposition"
                 SaveRequested="PersistAsync"
                 StateChanged="ObserveState" />
```

The host must await persistence in `SaveRequested`, enforce the expected content revision, and call `SetPersistedRevision` when a new revision is available. An exception, including `FileSaveConflictException`, leaves the editor dirty. `StateChanged` supplies the dirty/saving/conflict state needed by close guards and surrounding window chrome.

## Retention and caching boundary

FileBrowser session retention is a bounded UI-navigation optimization, not an application storage cache. Use `Disabled` for live folders such as agent/process output. Use `Bounded` only when revisiting loaded pages is desirable, and call the session invalidation or refresh APIs when freshness changes. Expensive aggregate, project, remote, or content-addressed caching belongs in a host adapter outside FileTools; authorization must still be re-applied on every effect.

The included filesystem provider also implements `IFileContentSource`, so an authorized host can reuse it for read-only FileInteraction content without involving the browser session. The host still mints the `FileReference`; a browser item key is not authority, and the provider intentionally supplies no write target or optimistic-concurrency revision for mutable local files.

## Ownership and migration

FileBrowser ownership moved from `CanDoItAll.Components` to this standalone repository. The migration is a breaking package and namespace change:

| Previous package | Replacement |
| --- | --- |
| `CanDoItAll.Components.FileBrowser.Core` | `CanDoItAll.FileTools.Abstractions` plus `CanDoItAll.FileTools.FileBrowser.Core` |
| `CanDoItAll.Components.FileBrowser.BaseLib` | `CanDoItAll.FileTools.FileBrowser.Components` |
| `CanDoItAll.Components.FileBrowser.Providers.FileSystem` | `CanDoItAll.FileTools.Providers.FileSystem` |

The new browser RCL does not depend on Components.BaseLib. Do not load the former global FileBrowser stylesheet or old `_content/CanDoItAll.Components.FileBrowser.BaseLib/...` paths; the new RCL uses isolated/collocated assets. `CanDoItAll.Components` continues to own simple general-purpose wrappers such as Mermaid, which hosts may register through the FileInteraction renderer seam.

## Packaging

Build all eight NuGet and symbol packages through the repository adapter, then inspect
their dependency, metadata, readme, symbols, static assets, and hashes:

```powershell
.\tools\deployment\nugets\Build-NuGets.ps1 -Configuration Release
.\tools\validation\Test-NuGetPackages.ps1
```

The tools write ignored local artifacts and never publish packages. See the
[build and packaging guide](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/build-and-packaging.md)
for `-NoRestore`, `-NoBuild`, version overrides, and CI usage.

## Documentation

- [Package architecture and dependency direction](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/package-architecture.md)
- [FileBrowser and provider guide](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/file-browser.md)
- [FileInteraction extension guide](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/file-interaction.md)
- [Native file-launching boundary](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/architecture/native-file-launching.md)
- [Host integration and security](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/host-integration-security.md)
- [Build, test, pack, and validate](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/build-and-packaging.md)

The Sandbox under `samples/CanDoItAll.FileTools.Sandbox` demonstrates composed UI scenarios. The separate integration architecture bundle is prepared for future CanDoItAll work; none of that future host-module wiring is claimed as shipped by these packages.

## License and contributions

The repository uses the
[MIT License](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/LICENSE).

Code contributions are limited to partners approved by the maintainer. See the
[contribution policy](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/CONTRIBUTING.md)
and contact the `fyziktom` account on LinkedIn before opening a pull request. Security
reports must follow the
[security policy](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/SECURITY.md).
