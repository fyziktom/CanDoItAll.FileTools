# CanDoItAll.FileTools.Sandbox

| Setting | Value |
|---------|-------|
| **Interactivity Mode** | Server |
| **Interactivity Scope** | Per-page |

## Rendering configuration

This project uses per-page Interactive Server with prerendering. Pages are static SSR by default. Only components that explicitly add `@rendermode InteractiveServer` become interactive.

## Adding new components

- Create routable pages in `Components/Pages/` and shared components in `Components/`.
- Opt into Interactive Server only where live browser controls require it.
- Keep data flowing down through parameters and events flowing up through `EventCallback<T>`.

## Data access

Interactive Server components may inject the sample's server-side provider factory directly. The live filesystem source must stay confined to the generated sandbox root.

The host may open a `FileInteraction` only after deliberately mapping an authorized sandbox item key to an opaque `FileReference`. Reads must use bounded fresh streams. Editable demonstrations use the sandbox-local in-memory gateway with optimistic revisions; that gateway is a host adapter example, not a FileTools storage provider.

## Sibling CanDoItAll.Components

- `CanDoItAll.FileTools.Sandbox.csproj` references the sibling `..\..\..\CanDoItAll.Components` checkout (BaseLib and Mermaid) as direct project references when it exists, or the directory named by the `CanDoItAllComponentsRoot` MSBuild property. Never replace that link with NuGet packages.
- Components-dependent Razor files live under `Components/ComponentsIntegration/` and are excluded from the build when the sibling is absent. `SandboxComponentsSupport` exposes the optional component types so the rest of the sandbox never references Components namespaces directly.
- `App.razor` loads the BaseLib Material Symbols, Noto font, and Tailwind stylesheets plus `MermaidHeadAssets` through that seam. The Mermaid sample renders `MermaidDiagram` with its own pan/zoom disabled inside the FileTools `FileZoomPanFrame`, so viewport behavior is proven against the shipped FileTools implementation.
- Shipping FileTools packages must not reference Components; keep the sibling checkout read-only from this repository.

## Environment constraints

- Interactive components run on the server through SignalR.
- Do not depend on `HttpContext` during a circuit.
- Do not introduce browser-global JavaScript for sandbox behavior.
- File interaction windows must remain bounded and keep scrolling inside their own workspace.
- Unknown or unmapped content identities remain inert.

## Don'ts

- Do not make render mode global in `App.razor`.
- Do not add application-specific behavior to the reusable FileBrowser RCL.
- Do not execute file actions from the sandbox host; log `ItemInvoked` and `ActionRequested` instead.
- Do not open operating-system applications, navigate the browser, download, copy, use the clipboard, fetch remote content, or perform storage side effects from an item action. Authorized Open actions may only display the in-page sandbox `FileInteraction`.
- Do not let the reusable interaction component persist directly. The sandbox host must explicitly handle and await save requests.
