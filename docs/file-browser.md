# FileBrowser and provider guide

## Compose a browser session

Implement or select one `IFileBrowserProvider` per source, then construct `FileBrowserSession` from those providers. Source IDs must be unique. For a source set that changes at runtime, construct a `FileBrowserSourceSet` with a stable revision and call `UpdateSourcesAsync`; a stale, fixed provider list is not sufficient for changing project/resource filters.

`FileBrowserSessionOptions` controls page size, requested metadata, default sort, and state retention. The session exposes initialization, source/folder navigation, paging, search, refresh/retry, selection, item/source/all invalidation, content reads, and provider-action delegation.

## Provider contract

Every provider implements three shallow, bounded operations:

- `GetRootAsync` returns the source root occurrence.
- `GetPathAsync` returns the root-to-occurrence path for navigation and breadcrumbs.
- `BrowseAsync` returns one direct-child page with an opaque continuation token.

Implement optional facets only when the backend supports them:

- `IFileBrowserSearchProvider` for provider-native/indexed search;
- `IFileBrowserContentProvider` for bounded or range-aware reads;
- `IFileBrowserActionProvider` for explicitly delegated custom actions.

Advertise only executable capabilities in `FileBrowserSourceDescriptor` and item capabilities. Capability flags control affordances; the host still authorizes every effect.

`IncludeDescendants` is deliberately generic. Advertise `RecursiveBrowse` only when the provider can honor it; the included filesystem provider remains shallow and rejects recursive requests. A project host can give the component a domain label such as "Include subprojects" without putting project semantics into FileTools.

## Provider correctness

- Item keys are opaque, stable occurrence identifiers scoped by source. Do not make the core parse filesystem paths, CIDs, FTP handles, or database keys.
- Continuation tokens are opaque and bound to the query. Return a consistency token when it can detect paging against changed data; reject stale cursors rather than mixing snapshots.
- Report completeness and non-fatal page warnings honestly. Do not turn a partial enumeration into a complete one.
- Honor requested metadata, sorting, filters, page limits, and cancellation. Translate expected backend failures into renderer-safe `FileBrowserProviderException` errors; do not render credentials, roots, exception paths, or SDK details.
- `DisplayPath`, open/download metadata, and action results are display/routing hints only. They are not executable authority.

## Included filesystem provider

`FileSystemFileBrowserProvider` exposes one absolute, existing, non-reparse directory. It performs shallow direct-child enumeration, deterministic paging, fresh metadata reads, and bounded/range file reads. It implements both `IFileBrowserContentProvider` and `IFileContentSource`, so the same read-only adapter can serve Browser and FileInteraction after the host authorizes a handoff. Child reparse points are either excluded or exposed as inert links and are never followed. Hidden entries are excluded by default.

```csharp
var options = new FileSystemFileBrowserOptions(
    new FileBrowserSourceId("run-artifacts"),
    authorizedAbsoluteRoot,
    displayName: "Run artifacts",
    includeHidden: false,
    reparsePointPolicy: FileSystemReparsePointPolicy.Exclude,
    recommendedPageSize: 50,
    maximumPageSize: 250);

var provider = new FileSystemFileBrowserProvider(options);
await using IFileBrowserSession session = new FileBrowserSession(
    [provider],
    new FileBrowserSessionOptions(
        retentionMode: FileBrowserStateRetentionMode.Disabled));
```

The root confines the provider, but the application must authorize that root before constructing the options. For FileInteraction, the host issues a `FileReference` whose source ID matches the configured provider and whose opaque value is the canonical root-relative occurrence key. Do that only after current authorization; copying a browser key does not itself grant access. The returned local-file lease intentionally has no optimistic-concurrency revision, and the provider supplies no save/write implementation.

The filesystem adapter does not watch the directory and does not use an application cache. `RefreshAsync` asks it again; `Disabled` retention is the normal choice for actively changing process/agent folders.

## UI contract

`FileBrowser` supports list/card projections and Standard, Compact, and Minimal chrome. It handles folder navigation internally. File activation emits `ItemInvoked`; action affordances emit `ActionRequested`. The host may use these events to open FileInteraction, a dialog, an OS explorer, a download endpoint, or another authorized workflow.

Do not call `ExecuteActionAsync` merely because an action was displayed. Re-resolve access first. If the host deliberately delegates a provider action, await the call and apply any returned URI/value only through a host policy.
