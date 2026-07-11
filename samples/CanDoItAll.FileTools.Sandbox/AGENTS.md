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
