# Package architecture

FileTools separates neutral contracts, runtime policy, UI, and optional adapters so small applications can stop at the narrowest useful package.

```text
Abstractions
|-- FileBrowser.Core
|   `-- FileBrowser.Components
|-- Providers.FileSystem
`-- FileInteraction.Core
    `-- FileInteraction.Components
        `-- FileInteraction.Markdown

Desktop
```

`FileInteraction.Markdown` also references Abstractions and FileInteraction.Core directly. The diagram shows the intended direction, not every transitive edge.

## Boundary rules

- Abstractions is BCL-only and defines provider, item, content, interaction, save, history, and profile contracts.
- Core packages contain deterministic policy and runtime coordination. They do not reference Blazor, a concrete storage SDK, or CanDoItAll application assemblies.
- Component packages are Razor class libraries over the corresponding core. They use the ASP.NET Core shared framework and isolated or collocated static assets.
- Providers are leaf adapters into Abstractions. The included filesystem provider does not reference either UI package.
- Desktop is an independent host-side adapter over the operating-system process boundary; UI packages consume it only through host callbacks.
- Optional renderers extend the explicit `FileInteractionComponentBuilder`. Markdig is present only in the Markdown package.
- Samples and tests are composition/verification projects and are never package dependencies.

## Runtime ownership

The host owns provider and browser-session lifetimes. An `IFileBrowserSession` publishes immutable snapshots and serializes navigation, search, refresh, selection, and invalidation operations. The FileBrowser component subscribes to a supplied session; it is not a service locator and does not own application storage.

The host also owns `IFileContentSource`, persistence, and authorization. FileInteraction selects immutable profile/renderer composition for a request, manages editing policy, and emits an awaited save callback. It does not open a browser item or resolve a storage driver.

## Identity boundaries

`FileBrowserItemKey` identifies an occurrence inside one browser source. Optional content identity identifies immutable bytes and is not a substitute for occurrence identity. `FileReference` is an opaque interaction handle. Neither display paths, URIs, capability flags, nor references are permission grants.

## Dependency policy enforcement

`tools/validation/Test-NuGetPackages.ps1` verifies both project references and packed
dependency metadata against the eight-project manifest. It supports independently
versioned packages, rejects Components/main-application dependencies, confines Markdig to
the Markdown adapter, checks package provenance and RCL assets, and requires assemblies
plus XML documentation in every package.
