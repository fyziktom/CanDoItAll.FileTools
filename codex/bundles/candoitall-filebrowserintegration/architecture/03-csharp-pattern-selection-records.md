# C# Pattern Selection Records

## PSR-01 — Adapter for storage/file sources

- Force: filesystem, FTP, IPFS, projects, process artifacts, and resources expose different APIs and must not leak into FileTools.
- Selected: Adapter behind `IFileBrowserProvider` plus optional content/action capabilities.
- Rejected: one universal concrete driver, inheritance from CanDoItAll `IStorageDriver`, reflection mapping.
- Test seam: fake provider and per-adapter contract suites.
- Anti-monolith proof: adding FTP/project adapters does not edit FileBrowser session or components.

## PSR-02 — Catalog plus deterministic resolver

- Force: runtime source/profile/renderer selection with multiple implementations and priority rules.
- Selected: immutable catalog/factory method with deterministic scoring and explicit ambiguity/unsupported results.
- Rejected: giant extension switch, `IServiceProvider` service location, assembly scanning by default.
- Test seam: catalogs constructed directly from fake descriptors.
- Anti-monolith proof: a new profile/renderer registers alongside existing entries without modifying the resolver.

## PSR-03 — Strategy for search, retention, autosave, preview, and history

- Force: algorithms/policies vary independently by provider/file type/use case.
- Selected: narrow strategies/policy records with focused implementations.
- Rejected: flags accumulated in `FileBrowserSession`/`FileInteraction` or mode switches in one component.
- Test seam: deterministic fake clock/delay and fake strategies.
- Anti-monolith proof: disabled retention and idle/edit-count/text-unit autosave have direct tests without constructing a Razor component.

## PSR-04 — Thin facade for browser and interaction sessions

- Force: callers need a stable state/event API while internal responsibilities are split.
- Selected: facade coordinating loader/navigation/search/selection/store or profile/save/preview/history collaborators.
- Rejected: keep 1,315-line session, nested helper classes, partial session files.
- Test seam: collaborators passed directly; facade behavior and each service tested separately.
- Anti-monolith proof: facade contains orchestration only and delegates behavior to top-level types.

## PSR-05 — Decorator for CanDoItAll listing cache

- Force: some semantic sources are expensive and cacheable; others require live reads; distributed backing may arrive later.
- Selected: host-side `HybridCachedFileBrowserProvider`/snapshot-cache decorator around source adapters.
- Rejected: cache in FileBrowser component/session, cache inside every storage driver, static global cache.
- Test seam: no-op/fake cache and revision provider.
- Boundary: this pattern is planned for CanDoItAll only and is not implemented in FileTools in this run.

## PSR-06 — Observer/event-up UI flow

- Force: hosts own open/save/download/promotion effects and need component notifications.
- Selected: immutable snapshots/Changed events in core and Blazor `EventCallback<T>` upward from components.
- Rejected: delegates as parameters, direct anchors from untrusted provider URIs, component service location.
- Test seam: bUnit/component contract tests and browser action assertions.

## PSR-07 — Renderer plug-in registry

- Force: file-type UI implementations have optional dependencies and assets.
- Selected: explicit renderer descriptors assembled through `FileInteractionComponentBuilder`; a host can pass the immutable composition directly or register that already-built composition as a singleton. The shell uses `DynamicComponent` only after neutral profile resolution.
- Rejected: all file types in one package, giant `switch(extension)`, runtime assembly scan.
- Test seam: fake renderer types, priority/ambiguity tests, composition smoke.
