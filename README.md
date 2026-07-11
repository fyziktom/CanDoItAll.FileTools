# CanDoItAll.FileTools

Storage-neutral .NET and Blazor building blocks for browsing, viewing, and editing file-like resources. The repository targets .NET 10 and keeps application storage drivers, authorization, application-triggered navigation, downloads, clipboard access, and persistence in the host. Browser-native viewer behavior, notably embedded PDF actions, remains browser-owned and is documented as an explicit boundary.

## Choose packages

| Need | Package | Direct dependencies |
| --- | --- | --- |
| Shared browser and interaction contracts only | `CanDoItAll.FileTools.Abstractions` | BCL only |
| Provider-neutral browsing, navigation, search, paging, retention, and invalidation | `CanDoItAll.FileTools.FileBrowser.Core` | Abstractions |
| Responsive Blazor browser in Standard, Compact, or Minimal mode | `CanDoItAll.FileTools.FileBrowser.Components` | Abstractions, FileBrowser.Core, ASP.NET Core shared framework |
| Root-confined local folder browsing plus Browser and Interaction range reads | `CanDoItAll.FileTools.Providers.FileSystem` | Abstractions |
| Profile resolution, save/autosave, preview, and bounded edit history | `CanDoItAll.FileTools.FileInteraction.Core` | Abstractions |
| Blazor interaction shell plus text, raster-image, browser-native PDF, inert SVG, and fallback renderers | `CanDoItAll.FileTools.FileInteraction.Components` | Abstractions, FileInteraction.Core, ASP.NET Core shared framework |
| Optional Markdown view/edit/preview renderer | `CanDoItAll.FileTools.FileInteraction.Markdown` | Abstractions, FileInteraction.Core, FileInteraction.Components, Markdig, ASP.NET Core shared framework |

Install only the right-hand packages needed by the host. In particular, a browser-only application does not acquire Markdig or interaction renderers, and a custom provider can depend on Abstractions without taking Blazor.

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

## Documentation

- [Package architecture and dependency direction](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/package-architecture.md)
- [FileBrowser and provider guide](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/file-browser.md)
- [FileInteraction extension guide](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/file-interaction.md)
- [Host integration and security](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/host-integration-security.md)
- [Build, test, pack, and validate](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/docs/build-and-packaging.md)

The Sandbox under `samples/CanDoItAll.FileTools.Sandbox` demonstrates composed UI scenarios. The separate integration architecture bundle is prepared for future CanDoItAll work; none of that future host-module wiring is claimed as shipped by these packages.

## License

MIT. See [LICENSE](https://github.com/fyziktom/CanDoItAll.FileTools/blob/main/LICENSE).
