# Host integration and security

FileTools intentionally stops before application authority. Providers describe occurrences and capabilities; components raise intent. The host performs authorization, stable handle resolution, navigation, clipboard access, downloads, OS integration, and persistence.

## Browser-to-interaction handoff

1. Receive `ItemInvoked` with a browser occurrence.
2. Resolve the current item through a trusted host adapter; do not trust stale display metadata.
3. Authorize the current principal against the intended operation and scope.
4. Mint or obtain an opaque `FileReference` plus `IFileContentSource` independent of browser-session lifetime. The included filesystem provider can be that read source after the host issues its root-relative reference; it does not turn the original browser key into authority.
5. Open FileInteraction with media/extension/size/revision hints.
6. On save, authorize again and enforce `ExpectedRevision` in the storage write.

Display paths, content identities, open/download URIs, provider action values, capability flags, and `FileReference.ToString()` are not bearer tokens. Keep secrets, absolute authorization roots, storage credentials, signed URLs, and database/SDK objects out of renderer-facing models and errors.

## Effects

- FileBrowser never executes a provider URI, writes the clipboard, opens the OS explorer, or starts a download.
- FileInteraction never persists directly. `SaveRequested` succeeds only when the host callback completes normally.
- Treat a conflict retry with a null `ExpectedRevision` as an overwrite request requiring explicit host policy and authorization. A retry against `ActualRevision` still resubmits local bytes; it is not a merge.
- Revalidate actions at execution time. A cached catalog entry or visible button is not proof that access still exists.
- Apply an origin/scheme policy to any host-approved navigation result. Avoid putting short-lived signed URLs into retained snapshots.

## Untrusted content

Treat file bytes, names, metadata, Markdown, warning/error text, and provider labels as untrusted. Razor text rendering is encoded by default. The base raster-image, PDF, and browser-frame components create component-owned object URLs and revoke them on replacement/disposal. SVG and arbitrary fallback content are routed to an `<iframe>` with an empty `sandbox` capability set and a `no-referrer` policy; payload bytes are never injected into the host document. This blocks scripts and privileged frame interactions, but iframe sandboxing is not a network-isolation boundary: embedded markup can still request subresources. A host that requires zero preview egress must provide a stricter renderer or isolated-origin policy. A host-supplied `FileObjectView.TargetFrame` may decorate the target but must render the supplied `TargetContent` unchanged so the sandbox remains the document boundary.

PDF is rendered through the browser's native `<object>` surface. Embedded PDF links/actions are browser/PDF-viewer behavior and do not pass through `ItemInvoked`, `ActionRequested`, or another FileInteraction host callback. Use a different renderer or external host flow when every navigation/action must be mediated by application authorization.

The optional Markdown adapter disables raw HTML and filters links/images so file content cannot create active or ambient-fetch URLs. If a host replaces that policy, it owns the complete sanitization and navigation decision. Raw HTML suppression alone is not a URL policy.

Type-specific JavaScript should remain collocated with its component, import dynamically, and clean up resources. CSS isolation scopes selectors but hosts should still avoid relying on package-internal class names as an integration API.

## Caching and authorization

Session `Bounded` retention reuses browser snapshots in one UI runtime. It is not a distributed or durable cache. For live filesystem/process output, use `Disabled`. For expensive project, IPFS, FTP, or resource aggregation, place caching in the host adapter and include all data-scope inputs in its cache key. Either cache pre-authorization data and reapply authorization for each caller, or include an authorization-scope fingerprint; never share post-authorization results across principals accidentally.

Use session invalidation APIs when a provider/source revision changes. A future host-level notification bridge may call those APIs, but FileTools does not ship CanDoItAll project timestamps, storage-driver settings, HybridCache configuration, or module-specific scope resolution.

## Filesystem boundary

The included provider confines traversal to the configured existing root and never follows reparse points. That is defense in depth, not the application's scope decision. Construct a provider only after the host has resolved and authorized the root. Residual filesystem time-of-check/time-of-use races must be considered when the application grants access to roots writable by less-trusted processes.
