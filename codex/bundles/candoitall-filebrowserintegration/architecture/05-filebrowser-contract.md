# FileBrowser Contract and Runtime Design

## Public boundary

The browser consumes a catalog of `IFileBrowserProvider` sources. Provider data is descriptive and capability-driven; it never grants host authorization by itself.

```text
FileBrowserSourceId + FileBrowserItemKey = occurrence identity
ContentIdentity                        = optional immutable/content identity
DisplayPath/OpenUri/DownloadUri         = metadata only
Capabilities                           = affordance eligibility, not execution authority
```

## Provider facets

- required shallow browse/root/path facet;
- optional native search facet;
- optional range-aware content facet;
- optional action-description/execution facet for hosts that explicitly delegate;
- optional freshness/version facet exposing immutable version/change token, never a cache implementation;
- optional source-update notification consumed by a host/session invalidation bridge.

## Activation rules

1. Pointer double-click, Enter, or explicit primary action on a navigable container calls session navigation.
2. The same gestures on a non-container emit `ItemInvoked` when invocation capability is present.
3. Open/download/copy/custom actions emit `ActionRequested`; no component-generated external anchor executes a provider URI.
4. The host revalidates current access and maps the item to OS explorer, dialog, FileInteraction, download endpoint, or resource promotion.

## Retention versus host cache

- `Disabled`: retain only the immutable current snapshot needed to render; each navigation/refresh asks the provider.
- `Bounded`: reuse bounded tree/pages for UI navigation and search; public invalidate-source/item/all operations exist.
- This session state is not the expensive CanDoItAll aggregate cache and never uses `IMemoryCache`.
- Process-run/filesystem sources default to Disabled in host integration; project composite/IPFS sources may use host HybridCache plus either session mode.

## Dynamic sources

The catalog/session accepts a source-set revision. When selected project filters or resource attachments change, the host replaces the catalog or calls `UpdateSourcesAsync`, preserving a location only if source/item identities still exist. Fixed construction with stale project sources is insufficient.

## Generic terminology

The base UI uses `Include descendants`, not `Include subprojects`. Hosts can provide a localized/semantic label. Project-specific semantics belong in the CanDoItAll wrapper.

## Session responsibility slices

- `FileBrowserLoader`: root/path/browse/page validation and cancellation.
- `FileBrowserNavigator`: history/up/source/deep initialization.
- `FileBrowserSearchCoordinator`: selected search strategy and continuation lifecycle.
- `FileBrowserSelectionState`: single/multi selection policy.
- `IFileBrowserStateStore`: disabled/bounded retained page state.
- `FileBrowserSession`: thin serialized operation facade, immutable snapshot publication, retry metadata.

